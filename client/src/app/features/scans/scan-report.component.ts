import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatRippleModule } from '@angular/material/core';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NgSelectModule } from '@ng-select/ng-select';
import { forkJoin } from 'rxjs';
import { ProjectSummary, ScanRun, Vulnerability } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-scan-report',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatRippleModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDividerModule,
    MatTooltipModule,
    NgSelectModule,
  ],
  templateUrl: './scan-report.component.html',
  styleUrl: './scan-report.component.scss',
  styles: `
    :host {
      display: block;
    }

    .report-page {
      max-width: 1200px;
      margin: 0 auto;
    }

    /* Toolbar -------------------------------------------------- */
    .report-toolbar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 20px;
      gap: 12px;
    }

    .toolbar-actions {
      display: flex;
      gap: 8px;
    }

    /* Cover ---------------------------------------------------- */
    .cover {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 24px;
      padding: 28px 32px;
      margin-bottom: 24px;
      background: var(--gradient-brand);
      color: #fff;
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-md);
    }

    .eyebrow {
      display: inline-block;
      font-size: 11px;
      font-weight: 600;
      letter-spacing: 0.12em;
      text-transform: uppercase;
      padding: 4px 10px;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.18);
      margin-bottom: 12px;
    }

    .cover h1 {
      font-size: 28px;
      font-weight: 700;
      margin: 0 0 6px;
      letter-spacing: -0.01em;
    }

    .cover .subtitle {
      margin: 0;
      opacity: 0.9;
      font-size: 14px;
    }

    .cover-stamp {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 8px;
      flex-shrink: 0;
    }

    .duration, .trigger { font-size: 12px; opacity: 0.85; }

    /* Summary cards -------------------------------------------- */
    .summary-grid {
      display: grid;
      grid-template-columns: repeat(6, 1fr);
      gap: 14px;
      margin-bottom: 24px;
    }

    .summary-card {
      background: var(--surface-card);
      border-radius: var(--radius-md);
      padding: 16px;
      display: flex;
      align-items: center;
      gap: 14px;
      border: 1px solid var(--neutral-200);
      box-shadow: var(--shadow-xs);
    }

    .sc-icon {
      width: 42px;
      height: 42px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }

    .sc-icon mat-icon {
      font-size: 22px;
      width: 22px;
      height: 22px;
    }

    .summary-card.critical .sc-icon { background: var(--sev-critical-bg); color: var(--sev-critical); }
    .summary-card.high .sc-icon { background: var(--sev-high-bg); color: var(--sev-high); }
    .summary-card.medium .sc-icon { background: var(--sev-medium-bg); color: var(--sev-medium); }
    .summary-card.low .sc-icon { background: var(--sev-low-bg); color: var(--sev-low); }
    .summary-card.total .sc-icon { background: var(--brand-teal-50); color: var(--brand-teal); }
    .summary-card.repos .sc-icon { background: var(--brand-navy-50); color: var(--brand-navy); }

    .sc-body {
      display: flex;
      flex-direction: column;
      line-height: 1.15;
    }

    .sc-value {
      font-size: 22px;
      font-weight: 700;
      color: var(--neutral-900);
    }

    .sc-label {
      font-size: 12px;
      color: var(--neutral-500);
      margin-top: 2px;
      letter-spacing: 0.02em;
    }

    /* Distribution bar ----------------------------------------- */
    .distribution {
      margin-bottom: 24px;
    }

    .bar {
      height: 14px;
      border-radius: 7px;
      overflow: hidden;
      display: flex;
      background: var(--neutral-200);
    }

    .bar-segment.critical, .dot.critical, .sev-chip.critical { background: var(--sev-critical); }
    .bar-segment.high, .dot.high, .sev-chip.high { background: var(--sev-high); }
    .bar-segment.medium, .dot.medium, .sev-chip.medium { background: var(--sev-medium); }
    .bar-segment.low, .dot.low, .sev-chip.low { background: var(--sev-low); }

    .bar-legend {
      display: flex;
      gap: 18px;
      margin-top: 14px;
      flex-wrap: wrap;
    }

    .legend-item {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      font-size: 12px;
      color: var(--neutral-700);
    }

    .dot { width: 10px; height: 10px; border-radius: 50%; }

    .all-clear {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 16px 18px;
      background: var(--brand-teal-50);
      border-radius: var(--radius-md);
      color: var(--brand-teal-700);
    }

    .all-clear mat-icon {
      font-size: 32px;
      width: 32px;
      height: 32px;
    }

    .all-clear strong { display: block; font-size: 14px; }
    .all-clear span { font-size: 12px; opacity: 0.8; }

    /* Projects summary (collapsible) -------------------------- */
    .projects-summary {
      margin-bottom: 24px;
      overflow: hidden;
    }

    .expand-toggle {
      width: 100%;
      background: none;
      border: none;
      padding: 16px 20px;
      cursor: pointer;
      display: block;
      text-align: left;
    }

    .expand-toggle:hover {
      background: var(--neutral-100);
    }

    .summary-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }

    .summary-info {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .summary-info mat-icon {
      color: var(--brand-teal);
      font-size: 24px;
      width: 24px;
      height: 24px;
    }

    .summary-info strong {
      display: block;
      font-size: 14px;
      font-weight: 600;
      color: var(--neutral-900);
    }

    .summary-info .sub {
      display: block;
      font-size: 12px;
      color: var(--neutral-600);
      margin-top: 2px;
    }

    .chevron {
      transition: transform 0.2s ease;
      color: var(--neutral-500);
    }

    .chevron.expanded {
      transform: rotate(180deg);
    }

    .project-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
      gap: 10px;
      padding: 0 20px 20px;
      border-top: 1px solid var(--neutral-200);
      margin-top: 8px;
      padding-top: 16px;
    }

    .project-tile {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 12px 14px;
      background: var(--neutral-100);
      border: 1px solid var(--neutral-200);
      border-radius: var(--radius-md);
      text-align: left;
      cursor: pointer;
    }

    .project-tile:hover {
      background: var(--brand-teal-50);
      border-color: var(--brand-teal-600);
    }

    .project-tile.selected {
      background: var(--brand-teal-50);
      border-color: var(--brand-teal);
      border-width: 2px;
      padding: 11px 13px;
    }

    .proj-header {
      display: flex;
      align-items: center;
      gap: 7px;
      font-size: 13px;
      font-weight: 600;
    }

    .proj-header mat-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
      color: var(--brand-navy);
    }

    .proj-stats {
      display: flex;
      gap: 12px;
      font-size: 11px;
      color: var(--neutral-600);
    }

    .proj-stats .stat {
      display: inline-flex;
      align-items: center;
      gap: 3px;
    }

    .proj-stats mat-icon {
      font-size: 13px;
      width: 13px;
      height: 13px;
    }

    .proj-severity {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
    }

    .sev-chip {
      display: inline-block;
      padding: 2px 6px;
      border-radius: 3px;
      font-size: 9px;
      font-weight: 700;
      letter-spacing: 0.03em;
      color: #fff;
    }

    /* Findings table ------------------------------------------- */
    .findings {
      margin-bottom: 24px;
    }

    .findings-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      width: 100%;
      gap: 20px;
    }

    .filter-badge {
      display: inline-flex;
      align-items: center;
      padding: 3px 10px;
      border-radius: 999px;
      background: var(--brand-teal-50);
      color: var(--brand-teal-700);
      font-size: 11px;
      font-weight: 700;
      margin-left: 8px;
      vertical-align: middle;
    }

    .findings-actions {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .filter-field {
      width: 160px;
      font-size: 13px;
    }

    .modern-table {
      width: 100%;
      background: transparent;
    }

    .modern-table th.mat-mdc-header-cell {
      font-weight: 600;
      color: var(--neutral-700);
      letter-spacing: 0.02em;
      font-size: 11px;
      text-transform: uppercase;
      background: var(--neutral-100);
    }

    .modern-table td.mat-mdc-cell,
    .modern-table th.mat-mdc-header-cell {
      padding: 12px 14px;
      border-bottom: 1px solid var(--neutral-200);
      font-size: 13px;
    }

    .text-muted {
      color: var(--neutral-500);
      margin-left: 6px;
      font-size: 12px;
    }

    .cve-link, .cve-text, .fix-version { font-family: 'SF Mono', Menlo, monospace; font-size: 12px; }
    .cve-link { color: var(--brand-teal-700); text-decoration: none; font-weight: 600; }
    .cve-link:hover { text-decoration: underline; }
    .fix-version { color: var(--brand-teal-700); }

    .sev-pill {
      display: inline-block;
      padding: 3px 10px;
      border-radius: 999px;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.02em;
      text-transform: capitalize;
    }

    .sev-pill.critical { background: var(--sev-critical-bg); color: var(--sev-critical); }
    .sev-pill.high { background: var(--sev-high-bg); color: var(--sev-high); }
    .sev-pill.medium { background: var(--sev-medium-bg); color: var(--sev-medium); }
    .sev-pill.low { background: var(--sev-low-bg); color: var(--sev-low); }

    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 3px 10px;
      border-radius: 999px;
      font-size: 11px;
      font-weight: 600;
      text-transform: capitalize;
    }

    .status-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
    }

    .status-badge.completed, .status-badge.open { background: var(--status-success-bg); color: var(--status-success); }
    .status-badge.running, .status-badge.queued { background: var(--status-info-bg); color: var(--status-info); }
    .status-badge.failed, .status-badge.unresolved { background: var(--status-error-bg); color: var(--status-error); }
    .status-badge.resolved, .status-badge.fixed { background: var(--brand-teal-50); color: var(--brand-teal-700); }
    .status-badge.suppressed, .status-badge.acknowledged { background: var(--status-warn-bg); color: var(--status-warn); }

    /* Misc ----------------------------------------------------- */
    .loading-container {
      display: flex;
      justify-content: center;
      padding: 64px;
    }

    .loading-container.small {
      padding: 24px;
    }

    .error-card {
      text-align: center;
      color: var(--status-error);
    }

    .error-card mat-icon {
      vertical-align: middle;
      margin-right: 8px;
    }

    .error-log pre {
      white-space: pre-wrap;
      font-size: 12px;
      color: var(--neutral-700);
      background: var(--neutral-100);
      padding: 12px;
      border-radius: var(--radius-sm);
      margin: 0;
    }

    .report-footer {
      text-align: center;
      font-size: 11px;
      color: var(--neutral-500);
      padding: 24px 0;
    }

    @media (max-width: 1024px) {
      .summary-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }

    @media (max-width: 600px) {
      .summary-grid {
        grid-template-columns: repeat(2, 1fr);
      }
      .cover {
        flex-direction: column;
        align-items: flex-start;
      }
      .cover-stamp {
        align-items: flex-start;
      }
    }
  `,
})
export class ScanReportComponent implements OnInit {
  readonly today = new Date();
  readonly loading = signal(true);
  readonly loadingVulns = signal(true);
  readonly downloading = signal(false);
  readonly rescanning = signal(false);
  readonly scan = signal<ScanRun | null>(null);
  readonly projects = signal<ProjectSummary[]>([]);
  readonly allVulns = signal<Vulnerability[]>([]);
  readonly errorMessage = signal('');

  // Filters
  readonly selectedProjectId = signal<string | null>(null);
  readonly selectedRepository = signal<string | null>(null);
  readonly projectsExpanded = signal(false);

  // Computed filtered vulnerabilities
  readonly vulns = computed(() => {
    let filtered = this.allVulns();
    const projId = this.selectedProjectId();
    const repo = this.selectedRepository();

    if (projId) {
      const project = this.projects().find(p => p.projectId === projId);
      if (project) {
        // Filter to vulns that belong to repos in this project
        // We need to check the projectName from vulnerability (not directly available)
        // For now, we'll use repository filtering
        filtered = filtered.filter(v => v.projectName === project.projectName);
      }
    }

    if (repo) {
      filtered = filtered.filter(v => v.repositoryName === repo);
    }

    return filtered;
  });

  // Unique repositories from all vulns (filtered by selected project if any)
  readonly repositories = computed(() => {
    const projId = this.selectedProjectId();
    let vulnsToFilter = this.allVulns();
    
    // If project is selected, only show repos from that project
    if (projId) {
      const project = this.projects().find(p => p.projectId === projId);
      if (project) {
        vulnsToFilter = vulnsToFilter.filter(v => v.projectName === project.projectName);
      }
    }
    
    const repos = new Set(vulnsToFilter.map(v => v.repositoryName));
    return Array.from(repos).sort();
  });

  // Selected project name for display
  readonly selectedProjectName = computed(() => {
    const projId = this.selectedProjectId();
    if (!projId) return null;
    return this.projects().find(p => p.projectId === projId)?.projectName ?? null;
  });

  readonly columns = ['severity', 'cve', 'package', 'fix', 'cvss', 'repo', 'status'];

  scanId = '';
  pendingProjectId: string | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly api: ApiService,
    private readonly snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('scanId');
    this.scanId = idParam ?? '';
    if (!this.scanId) {
      this.errorMessage.set('Invalid scan id.');
      this.loading.set(false);
      this.loadingVulns.set(false);
      return;
    }

    // Auto-apply filters from query params (e.g., navigated from scans tab with filtered project)
    const qp = this.route.snapshot.queryParamMap;
    const pid = qp.get('projectId');
    const repo = qp.get('repository');
    if (repo) this.selectedRepository.set(repo);
    // If projectId provided, we'll load and auto-select its project after scan loads
    this.pendingProjectId = pid ?? null;
    if (pid) this.selectedProjectId.set(pid);

    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.loadingVulns.set(true);

    forkJoin({
      scan: this.api.getScanById(this.scanId),
      projects: this.api.getProjectSummaries(this.scanId),
      vulns: this.api.getVulnerabilities({
        scanRunId: this.scanId,
        page: 1,
        pageSize: 250,
        sortBy: 'CvssScore',
        sortDescending: 'true',
      }),
    }).subscribe({
      next: ({ scan, projects, vulns }) => {
        this.loading.set(false);
        this.loadingVulns.set(false);
        if (scan.success && scan.data) {
          this.scan.set(scan.data);
          // If projectId was passed, auto-select its project
          if (this.pendingProjectId && projects.success && projects.data) {
            const matchingProject = projects.data.find(
              (p) => p.projectId === this.pendingProjectId,
            );
            if (matchingProject) {
              this.selectedProjectId.set(matchingProject.projectId);
            }
          }
        } else {
          this.errorMessage.set(scan.message ?? 'Scan not found.');
        }
        if (projects.success && projects.data) {
          this.projects.set(projects.data);
        }
        if (vulns.success && vulns.data) {
          this.allVulns.set(vulns.data.items);
        }
      },
      error: () => {
        this.loading.set(false);
        this.loadingVulns.set(false);
        this.errorMessage.set('Failed to load scan report.');
      },
    });
  }

  downloadPdf(): void {
    // Use the browser's print-to-PDF — produces high-quality, paginated output
    // and works without any external dependency.
    window.print();
  }

  rescan(): void {
    const s = this.scan();
    if (!s || !s.projectId || this.rescanning()) return;
    this.rescanning.set(true);
    this.api.triggerProjectScan(s.projectId).subscribe({
      next: (res) => {
        this.rescanning.set(false);
        const newId = res.data?.scanRunId;
        this.snackBar.open(
          newId ? `Re-scan started — new scan #${newId}` : 'Re-scan started',
          'View',
          { duration: 6000 },
        ).onAction().subscribe(() => {
          if (newId) {
            window.location.href = `/scans/${newId}/report`;
          }
        });
      },
      error: () => {
        this.rescanning.set(false);
        this.snackBar.open('Failed to trigger re-scan.', 'Close', { duration: 5000 });
      },
    });
  }

  downloadCsv(): void {
    const s = this.scan();
    if (!s) return;
    this.downloading.set(true);
    this.api.exportVulnerabilitiesCsv(undefined, this.scanId).subscribe({
      next: (blob) => {
        this.downloading.set(false);
        const filename = `scan-${s.id}-${(s.projectName ?? 'report').replace(/\s+/g, '_')}.csv`;
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        this.downloading.set(false);
        this.snackBar.open('Failed to export CSV.', 'Close', { duration: 5000 });
      },
    });
  }
}
