import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, computed, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectDetailReport, ReportVulnerability } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';
import { compareVersions } from '../../shared/version-compare';
import { formatEpss } from './intel/intel-format';

type ViewMode = 'cve' | 'package';
type SortKey = 'severity' | 'cve' | 'package' | 'repo' | 'cvss' | 'epss' | 'age' | 'count';
type FixState = 'none' | 'ok' | 'other-line';

const ALL = 'all';
const SEVERITIES = ['critical', 'high', 'medium', 'low'] as const;
const RANK: Record<string, number> = { critical: 4, high: 3, medium: 2, low: 1, negligible: 0 };
const CVE_SORTS: SortKey[] = ['severity', 'cve', 'package', 'repo', 'cvss', 'epss', 'age'];
const PACKAGE_SORTS: SortKey[] = ['severity', 'package', 'repo', 'cvss', 'count'];
const DESC_FIRST: SortKey[] = ['severity', 'cvss', 'epss', 'age', 'count'];

interface Finding extends ReportVulnerability {
  repoId: string;
  repoName: string;
  sev: string;
  rank: number;
  fix: FixState;
}

interface PackageGroup {
  key: string;
  name: string;
  version: string;
  ecosystem?: string;
  findings: Finding[];
  sev: string;
  rank: number;
  maxCvss: number | null;
  upgradeTo: string | null;
  unfixed: number;
  repoNames: string[];
  kev: boolean;
}

interface RailItem {
  id: string;
  name: string;
  findings: number;
  packages: number;
  vulnerablePackages: number;
  counts: Record<string, number>;
}

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatIconModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
})
export class ProjectDetailComponent implements OnInit {
  readonly pageSizeOptions = [10, 25, 50, 100];

  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly report = signal<ProjectDetailReport | null>(null);

  // View state — mirrored into the URL so a refresh, or Back from a CVE page, returns to the same place.
  readonly scope = signal<string>(ALL);
  readonly view = signal<ViewMode>('cve');
  readonly query = signal('');
  readonly fixableOnly = signal(false);
  readonly kevOnly = signal(false);
  readonly selectedSeverities = signal<ReadonlySet<string>>(new Set());
  readonly sort = signal<{ key: SortKey; dir: 'asc' | 'desc' }>({ key: 'severity', dir: 'desc' });
  readonly pageIndex = signal(0);
  readonly pageSize = signal(25);
  readonly expanded = signal<string | null>(null);

  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  projectId = '';
  private defaultScope = ALL;

  /** Every finding across the project, with the repository and fix state resolved once. */
  readonly allFindings = computed<Finding[]>(() => {
    const r = this.report();
    if (!r) return [];
    return r.repositories.flatMap((repo) =>
      repo.vulnerabilities.map((v) => {
        const sev = v.severity.toLowerCase();
        return {
          ...v,
          repoId: repo.repositoryId,
          repoName: repo.repositoryName,
          sev,
          rank: RANK[sev] ?? 0,
          fix: this.fixState(v),
        };
      }),
    );
  });

  readonly rail = computed<RailItem[]>(() => {
    const r = this.report();
    if (!r) return [];
    const all = this.allFindings();
    const countsOf = (rows: Finding[]) => {
      const counts: Record<string, number> = { critical: 0, high: 0, medium: 0, low: 0, negligible: 0 };
      for (const f of rows) counts[f.sev] = (counts[f.sev] ?? 0) + 1;
      return counts;
    };
    return [
      {
        id: ALL,
        name: 'All repositories',
        findings: all.length,
        packages: r.totalPackages,
        vulnerablePackages: r.repositories.reduce((n, x) => n + x.vulnerablePackages, 0),
        counts: countsOf(all),
      },
      ...r.repositories.map((repo) => ({
        id: repo.repositoryId,
        name: repo.repositoryName,
        findings: repo.vulnerabilities.length,
        packages: repo.totalPackages,
        vulnerablePackages: repo.vulnerablePackages,
        counts: countsOf(all.filter((f) => f.repoId === repo.repositoryId)),
      })),
    ];
  });

  readonly projectCounts = computed(() => this.rail()[0]?.counts ?? {});

  /** Findings in CISA's Known Exploited Vulnerabilities catalog: exploitation in the wild is confirmed. */
  readonly kevTotal = computed(() => this.allFindings().filter((f) => f.kev).length);
  readonly kevInScope = computed(() => this.inScope().filter((f) => f.kev).length);

  /** The four standard severities, plus Negligible only when the scan actually produced some. */
  readonly chipSeverities = computed<string[]>(() =>
    this.projectCounts()['negligible'] ? [...SEVERITIES, 'negligible'] : [...SEVERITIES],
  );

  /** Raw rows minus unique findings: how many rows were repeats of the same package in another manifest. */
  readonly repeatedHits = computed(() => {
    const r = this.report();
    return r ? Math.max(0, r.totalVulnerabilities - r.uniqueVulnerabilities) : 0;
  });

  readonly scopeName = computed(() => this.rail().find((i) => i.id === this.scope())?.name ?? 'All repositories');
  readonly scopeTotal = computed(() => this.rail().find((i) => i.id === this.scope())?.findings ?? 0);

  private readonly inScope = computed(() =>
    this.scope() === ALL ? this.allFindings() : this.allFindings().filter((f) => f.repoId === this.scope()),
  );

  /** Scope + search + "fix available"; severity is applied after so the severity chips can show their counts. */
  private readonly searched = computed(() => {
    const q = this.query().trim().toLowerCase();
    const fixable = this.fixableOnly();
    const kevOnly = this.kevOnly();
    return this.inScope().filter((f) => {
      if (fixable && f.fix !== 'ok') return false;
      if (kevOnly && !f.kev) return false;
      if (!q) return true;
      return (
        f.cveId.toLowerCase().includes(q) ||
        f.packageName.toLowerCase().includes(q) ||
        f.repoName.toLowerCase().includes(q) ||
        f.sourceFiles.some((s) => s.toLowerCase().includes(q))
      );
    });
  });

  readonly severityCounts = computed(() => {
    const counts: Record<string, number> = { critical: 0, high: 0, medium: 0, low: 0, negligible: 0 };
    for (const f of this.searched()) counts[f.sev] = (counts[f.sev] ?? 0) + 1;
    return counts;
  });

  private readonly filtered = computed(() => {
    const sel = this.selectedSeverities();
    return sel.size ? this.searched().filter((f) => sel.has(f.sev)) : this.searched();
  });

  readonly sortedFindings = computed(() => {
    const { key, dir } = this.sort();
    const sign = dir === 'asc' ? 1 : -1;
    const cmp = (a: Finding, b: Finding): number => {
      switch (key) {
        case 'cve': return a.cveId.localeCompare(b.cveId, undefined, { numeric: true });
        case 'package': return a.packageName.localeCompare(b.packageName) || compareVersions(a.installedVersion, b.installedVersion);
        case 'repo': return a.repoName.localeCompare(b.repoName);
        case 'cvss': return (a.cvssScore ?? -1) - (b.cvssScore ?? -1);
        case 'epss': return (a.epss ?? -1) - (b.epss ?? -1);
        case 'age': return a.ageDays - b.ageDays;
        // Within a severity, what is being exploited comes first, then the higher score.
        default: return a.rank - b.rank || Number(a.kev) - Number(b.kev) || (a.cvssScore ?? -1) - (b.cvssScore ?? -1);
      }
    };
    return [...this.filtered()].sort(
      (a, b) => sign * cmp(a, b) || a.cveId.localeCompare(b.cveId, undefined, { numeric: true }) || a.packageName.localeCompare(b.packageName),
    );
  });

  readonly groups = computed<PackageGroup[]>(() => {
    const byKey = new Map<string, Finding[]>();
    for (const f of this.filtered()) {
      const key = `${f.packageName}\u0000${f.installedVersion}`;
      (byKey.get(key) ?? byKey.set(key, []).get(key)!).push(f);
    }
    const groups = [...byKey.entries()].map(([key, findings]): PackageGroup => {
      const worst = findings.reduce((a, b) => (b.rank > a.rank ? b : a));
      const fixes = findings.filter((f) => f.fix === 'ok').map((f) => f.fixedVersion!);
      const scores = findings.map((f) => f.cvssScore).filter((s): s is number => s != null);
      return {
        key,
        name: findings[0].packageName,
        version: findings[0].installedVersion,
        ecosystem: findings.find((f) => f.ecosystem)?.ecosystem,
        findings: [...findings].sort((a, b) => b.rank - a.rank || Number(b.kev) - Number(a.kev) || (b.cvssScore ?? -1) - (a.cvssScore ?? -1)),
        sev: worst.sev,
        rank: worst.rank,
        maxCvss: scores.length ? Math.max(...scores) : null,
        upgradeTo: fixes.length ? fixes.reduce((a, b) => (compareVersions(b, a) > 0 ? b : a)) : null,
        unfixed: findings.filter((f) => f.fix !== 'ok').length,
        repoNames: [...new Set(findings.map((f) => f.repoName))].sort(),
        kev: findings.some((f) => f.kev),
      };
    });
    const { key, dir } = this.sort();
    const sign = dir === 'asc' ? 1 : -1;
    const cmp = (a: PackageGroup, b: PackageGroup): number => {
      switch (key) {
        case 'package': return a.name.localeCompare(b.name) || compareVersions(a.version, b.version);
        case 'repo': return (a.repoNames[0] ?? '').localeCompare(b.repoNames[0] ?? '');
        case 'cvss': return (a.maxCvss ?? -1) - (b.maxCvss ?? -1);
        case 'count': return a.findings.length - b.findings.length;
        default: return a.rank - b.rank || Number(a.kev) - Number(b.kev) || a.findings.length - b.findings.length;
      }
    };
    return groups.sort((a, b) => sign * cmp(a, b) || a.name.localeCompare(b.name));
  });

  readonly listLength = computed(() => (this.view() === 'cve' ? this.sortedFindings().length : this.groups().length));
  private readonly lastPage = computed(() => Math.max(0, Math.ceil(this.listLength() / this.pageSize()) - 1));
  readonly page = computed(() => Math.min(this.pageIndex(), this.lastPage()));

  readonly pagedFindings = computed(() => this.slice(this.sortedFindings()));
  readonly pagedGroups = computed(() => this.slice(this.groups()));

  readonly filtersActive = computed(
    () => !!this.query().trim() || this.fixableOnly() || this.kevOnly() || this.selectedSeverities().size > 0,
  );

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly apiService: ApiService,
  ) {}

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('projectId') ?? '';
    this.loadReport();
  }

  // ── View state ─────────────────────────────────────────────────

  selectScope(id: string): void {
    this.scope.set(id);
    this.resetPaging();
    this.syncUrl();
  }

  setView(mode: ViewMode): void {
    this.view.set(mode);
    const valid = mode === 'cve' ? CVE_SORTS : PACKAGE_SORTS;
    if (!valid.includes(this.sort().key)) this.sort.set({ key: 'severity', dir: 'desc' });
    this.resetPaging();
    this.syncUrl();
  }

  setQuery(value: string): void {
    this.query.set(value);
    this.resetPaging();
    this.syncUrl();
  }

  toggleFixable(): void {
    this.fixableOnly.update((v) => !v);
    this.resetPaging();
    this.syncUrl();
  }

  toggleKev(): void {
    this.kevOnly.update((v) => !v);
    this.resetPaging();
    this.syncUrl();
  }

  toggleSeverity(sev: string): void {
    this.selectedSeverities.update((cur) => {
      const next = new Set(cur);
      if (!next.delete(sev)) next.add(sev);
      return next;
    });
    this.resetPaging();
    this.syncUrl();
  }

  resetFilters(): void {
    this.query.set('');
    this.fixableOnly.set(false);
    this.kevOnly.set(false);
    this.selectedSeverities.set(new Set());
    this.resetPaging();
    this.syncUrl();
  }

  toggleSort(key: SortKey): void {
    const cur = this.sort();
    // First click puts the most important rows on top: worst severity / highest score / oldest / most CVEs.
    const firstDir = DESC_FIRST.includes(key) ? 'desc' : 'asc';
    this.sort.set(cur.key === key ? { key, dir: cur.dir === 'asc' ? 'desc' : 'asc' } : { key, dir: firstDir });
    this.pageIndex.set(0);
  }

  ariaSort(key: SortKey): 'ascending' | 'descending' | 'none' {
    const s = this.sort();
    return s.key !== key ? 'none' : s.dir === 'asc' ? 'ascending' : 'descending';
  }

  onPage(e: PageEvent): void {
    this.pageIndex.set(e.pageIndex);
    this.pageSize.set(e.pageSize);
    this.expanded.set(null);
    // The paginator sits below the rows; bring the top of the new page into view.
    this.panel()?.nativeElement.scrollIntoView({ block: 'start' });
  }

  toggleExpanded(id: string): void {
    this.expanded.update((cur) => (cur === id ? null : id));
  }

  // ── Presentation helpers ───────────────────────────────────────

  barWidth(item: RailItem, sev: string): number {
    return item.findings ? (item.counts[sev] / item.findings) * 100 : 0;
  }

  epssLabel(score: number | undefined | null): string {
    return score == null ? '—' : formatEpss(score);
  }

  ageLabel(days: number): string {
    return days <= 0 ? 'Today' : `${days}d`;
  }

  // ── Export ─────────────────────────────────────────────────────

  exportPackagesCsv(): void {
    this.apiService.exportProjectCsv(this.projectId).subscribe((blob) => {
      this.downloadBlob(blob, `${this.report()?.projectName ?? 'project'}-packages.csv`);
    });
  }

  exportVulnsCsv(): void {
    this.apiService.exportProjectVulnerabilitiesCsv(this.projectId).subscribe((blob) => {
      this.downloadBlob(blob, `${this.report()?.projectName ?? 'project'}-vulnerabilities.csv`);
    });
  }

  // ── Internals ──────────────────────────────────────────────────

  /** A "fix" older than the installed version means the advisory lists a patch on another release line. */
  private fixState(v: ReportVulnerability): FixState {
    if (!v.fixedVersion) return 'none';
    return compareVersions(v.fixedVersion, v.installedVersion) < 0 ? 'other-line' : 'ok';
  }

  private slice<T>(rows: T[]): T[] {
    const start = this.page() * this.pageSize();
    return rows.slice(start, start + this.pageSize());
  }

  private resetPaging(): void {
    this.pageIndex.set(0);
    this.expanded.set(null);
  }

  private restoreFromUrl(): void {
    const p = this.route.snapshot.queryParamMap;
    const repo = p.get('repo');
    const known = new Set(this.rail().map((i) => i.id));

    // Default to the repository with the most findings — the one that needs attention first.
    const worst = this.rail().slice(1).sort((a, b) => b.findings - a.findings)[0];
    this.defaultScope = worst && worst.findings > 0 ? worst.id : ALL;
    this.scope.set(repo && known.has(repo) ? repo : this.defaultScope);

    if (p.get('view') === 'package') this.view.set('package');
    this.query.set(p.get('q') ?? '');
    this.fixableOnly.set(p.get('fix') === '1');
    this.kevOnly.set(p.get('kev') === '1');
    const sevs = (p.get('sev') ?? '').split(',').filter((s) => s in RANK);
    this.selectedSeverities.set(new Set(sevs));
  }

  private syncUrl(): void {
    const sev = [...this.selectedSeverities()].sort((a, b) => RANK[b] - RANK[a]).join(',');
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParamsHandling: 'merge',
      queryParams: {
        repo: this.scope() === this.defaultScope ? null : this.scope(),
        view: this.view() === 'cve' ? null : this.view(),
        q: this.query().trim() || null,
        fix: this.fixableOnly() ? '1' : null,
        kev: this.kevOnly() ? '1' : null,
        sev: sev || null,
      },
    });
  }

  private loadReport(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.apiService.getProjectReport(this.projectId).subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data) {
          this.report.set(res.data);
          this.restoreFromUrl();
        } else {
          this.errorMessage.set(res.message ?? 'Project not found.');
        }
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load project report.');
      },
    });
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}
