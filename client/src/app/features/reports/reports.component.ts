import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
    EcosystemBreakdown,
    ProjectSummary,
    SeverityTrend,
    VulnerabilitySummaryItem,
} from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatTableModule,
    MatTabsModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatFormFieldModule,
    MatSelectModule,
    MatTooltipModule,
    MatSortModule,
  ],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
  styles: `
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .page-header h2 { margin: 0; font-weight: 500; }
    .header-actions { display: flex; gap: 8px; }

    .loading-container { display: flex; justify-content: center; padding: 48px; }

    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
      margin-bottom: 24px;
    }
    .kpi-card mat-card-content {
      display: flex; align-items: center; gap: 16px; padding: 16px !important;
    }
    .kpi-icon {
      width: 48px; height: 48px; border-radius: 12px;
      display: flex; align-items: center; justify-content: center;
    }
    .kpi-icon mat-icon { font-size: 28px; width: 28px; height: 28px; }
    .kpi-card.total-projects .kpi-icon { background: #e3f2fd; color: #1565c0; }
    .kpi-card.vulnerable-projects .kpi-icon { background: #fff3e0; color: #e65100; }
    .kpi-card.unique-cves .kpi-icon { background: #ffebee; color: #c62828; }
    .kpi-card.ecosystems .kpi-icon { background: #f3e5f5; color: #7b1fa2; }
    .kpi-data { display: flex; flex-direction: column; }
    .kpi-value { font-size: 28px; font-weight: 600; line-height: 1.2; }
    .kpi-label { font-size: 13px; color: rgba(0,0,0,0.54); }

    .ecosystem-card { margin-bottom: 24px; }
    .ecosystem-card mat-card-title { display: flex; align-items: center; gap: 8px; }
    .ecosystem-grid { display: flex; gap: 24px; flex-wrap: wrap; padding: 8px 0; }
    .ecosystem-item {
      padding: 12px 16px; border-radius: 8px; background: #f5f5f5;
      min-width: 200px; flex: 1;
    }
    .eco-name { font-weight: 600; font-size: 14px; margin-bottom: 8px; color: #1a237e; }
    .eco-stats { display: flex; flex-direction: column; gap: 4px; }
    .eco-stat { font-size: 13px; color: rgba(0,0,0,0.7); }
    .eco-stat.vulnerable.has-vulns { color: #c62828; font-weight: 500; }

    .tab-content { padding: 16px 0; }
    .tab-actions {
      display: flex; gap: 12px; align-items: center; margin-bottom: 16px;
    }
    .filter-field { width: 200px; }
    .filter-field ::ng-deep .mat-mdc-form-field-subscript-wrapper { display: none; }

    .full-width { width: 100%; }
    .report-table { border: 1px solid rgba(0,0,0,0.08); border-radius: 8px; overflow: hidden; }
    .report-table tr.mat-mdc-row:hover { background: rgba(0,0,0,0.04); cursor: pointer; }

    .project-link, .cve-link {
      color: #1565c0; font-weight: 500; cursor: pointer;
      text-decoration: none;
    }
    .project-link:hover, .cve-link:hover { text-decoration: underline; }

    .severity-badge {
      display: inline-block; padding: 2px 8px; border-radius: 12px;
      font-size: 12px; font-weight: 600; min-width: 24px; text-align: center;
    }
    .severity-badge.critical { background: #ffebee; color: #c62828; }
    .severity-badge.high { background: #fff3e0; color: #e65100; }
    .severity-badge.medium { background: #fff8e1; color: #f9a825; }
    .severity-badge.low { background: #e8f5e9; color: #2e7d32; }
    .severity-badge.zero { background: transparent; color: rgba(0,0,0,0.38); font-weight: 400; }

    .severity-chip {
      display: inline-block; padding: 4px 12px; border-radius: 16px;
      font-size: 12px; font-weight: 600; text-transform: uppercase;
    }
    .severity-chip.critical { background: #ffebee; color: #c62828; }
    .severity-chip.high { background: #fff3e0; color: #e65100; }
    .severity-chip.medium { background: #fff8e1; color: #f9a825; }
    .severity-chip.low { background: #e8f5e9; color: #2e7d32; }

    .fix-available { color: #2e7d32; font-weight: 500; }
    .no-fix { color: rgba(0,0,0,0.38); font-style: italic; }

    .vulnerable-row { background: rgba(255,235,238,0.3); }

    .no-data { text-align: center; padding: 24px; color: rgba(0,0,0,0.38); }
    .error-card { text-align: center; color: #c62828; }
    .error-card mat-icon { margin-right: 8px; vertical-align: middle; }

    /* Trend chart */
    .trend-chart {
      display: flex; gap: 12px; align-items: flex-end;
      justify-content: center; margin-top: 24px; padding: 16px;
      min-height: 200px;
    }
    .trend-bar-group { display: flex; flex-direction: column; align-items: center; gap: 4px; }
    .trend-bars { display: flex; gap: 2px; align-items: flex-end; }
    .trend-bar { width: 16px; border-radius: 4px 4px 0 0; min-height: 4px; transition: height 0.3s; }
    .trend-bar.critical { background: #c62828; }
    .trend-bar.high { background: #e65100; }
    .trend-bar.medium { background: #f9a825; }
    .trend-bar.low { background: #2e7d32; }
    .trend-label { font-size: 11px; color: rgba(0,0,0,0.54); }

    @media (max-width: 1200px) {
      .kpi-grid { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 768px) {
      .kpi-grid { grid-template-columns: 1fr; }
      .tab-actions { flex-wrap: wrap; }
    }
  `,
})
export class ReportsComponent implements OnInit {
  readonly loading = signal(true);
  readonly loadingVulns = signal(false);
  readonly errorMessage = signal('');

  readonly projects = signal<ProjectSummary[]>([]);
  readonly filteredProjects = signal<ProjectSummary[]>([]);
  readonly vulnerabilities = signal<VulnerabilitySummaryItem[]>([]);
  readonly filteredVulnerabilities = signal<VulnerabilitySummaryItem[]>([]);
  readonly trends = signal<SeverityTrend[]>([]);
  readonly ecosystemBreakdown = signal<EcosystemBreakdown[]>([]);

  readonly totalProjects = signal(0);
  readonly vulnerableProjects = signal(0);
  readonly uniqueCves = signal(0);

  readonly projectColumns = [
    'projectName', 'repositoryCount', 'totalPackages',
    'critical', 'high', 'medium', 'totalVulnerabilities', 'actions',
  ];
  readonly vulnColumns = [
    'cveId', 'severity', 'cvssScore', 'packageName',
    'fixedVersion', 'affectedRepositories', 'totalOccurrences', 'vulnActions',
  ];
  readonly trendColumns = [
    'scanDate', 'scanId', 'trendCritical', 'trendHigh',
    'trendMedium', 'trendLow', 'trendTotal',
  ];

  private maxTrendValue = 1;

  constructor(
    private readonly apiService: ApiService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.loadData();
  }

  refresh(): void {
    this.loadData();
  }

  onTabChange(index: number): void {
    if (index === 1 && this.vulnerabilities().length === 0) {
      this.loadVulnerabilities();
    }
    if (index === 2 && this.trends().length === 0) {
      this.loadTrends();
    }
  }

  filterProjects(filter: string): void {
    const all = this.projects();
    switch (filter) {
      case 'vulnerable':
        this.filteredProjects.set(all.filter(p => p.totalVulnerabilities > 0));
        break;
      case 'clean':
        this.filteredProjects.set(all.filter(p => p.totalVulnerabilities === 0));
        break;
      default:
        this.filteredProjects.set([...all]);
    }
  }

  sortProjects(sort: Sort): void {
    const data = [...this.filteredProjects()];
    if (!sort.active || sort.direction === '') {
      this.filteredProjects.set(data);
      return;
    }
    data.sort((a, b) => {
      const dir = sort.direction === 'asc' ? 1 : -1;
      const key = sort.active as keyof ProjectSummary;
      const aVal = a[key] ?? 0;
      const bVal = b[key] ?? 0;
      if (typeof aVal === 'string') return aVal.localeCompare(bVal as string) * dir;
      return ((aVal as number) - (bVal as number)) * dir;
    });
    this.filteredProjects.set(data);
  }

  filterBySeverity(severity: string): void {
    if (!severity) {
      this.loadVulnerabilities();
    } else {
      this.loadVulnerabilities(severity);
    }
  }

  openProjectDetail(projectId: string): void {
    this.router.navigate(['/reports/projects', projectId]);
  }

  openProjectConfig(projectId: string): void {
    this.router.navigate(['/reports/projects', projectId, 'config']);
  }

  openVulnDetail(cveId: string): void {
    this.router.navigate(['/reports/vulnerabilities', cveId]);
  }

  exportProjectCsv(projectId: string, projectName: string): void {
    this.apiService.exportProjectVulnerabilitiesCsv(projectId).subscribe(blob => {
      this.downloadBlob(blob, `${projectName}-vulnerabilities.csv`);
    });
  }

  exportAllVulnsCsv(): void {
    this.apiService.exportVulnerabilitiesCsv().subscribe(blob => {
      this.downloadBlob(blob, 'all-vulnerabilities.csv');
    });
  }

  barHeight(value: number): number {
    return Math.max(4, (value / this.maxTrendValue) * 120);
  }

  private loadData(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.apiService.getExecutiveSummary().subscribe({
      next: (res) => {
        this.loading.set(false);
        if (res.success && res.data) {
          const d = res.data;
          this.projects.set(d.projectSummaries);
          this.filteredProjects.set([...d.projectSummaries]);
          this.ecosystemBreakdown.set(d.ecosystemBreakdown);
          this.trends.set(d.severityTrend);
          this.totalProjects.set(d.totalProjects);
          this.vulnerableProjects.set(
            d.projectSummaries.filter(p => p.totalVulnerabilities > 0).length,
          );
          // Also set vulns from executive summary severity trend
          this.maxTrendValue = Math.max(1, ...d.severityTrend.map(t => t.total));
        } else {
          this.errorMessage.set(res.message ?? 'Failed to load report data.');
        }
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to connect to the API server.');
      },
    });
  }

  private loadVulnerabilities(severity?: string): void {
    this.loadingVulns.set(true);
    this.apiService.getVulnerabilitySummaries(severity).subscribe({
      next: (res) => {
        this.loadingVulns.set(false);
        if (res.success && res.data) {
          this.vulnerabilities.set(res.data);
          this.filteredVulnerabilities.set(res.data);
          this.uniqueCves.set(res.data.length);
        }
      },
      error: () => {
        this.loadingVulns.set(false);
      },
    });
  }

  private loadTrends(): void {
    this.apiService.getSeverityTrends().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.trends.set(res.data);
          this.maxTrendValue = Math.max(1, ...res.data.map(t => t.total));
        }
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
