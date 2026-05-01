import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectDetailReport } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatTableModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatExpansionModule,
    MatTooltipModule,
  ],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss',
  styles: `
    .page-header {
      display: flex; justify-content: space-between; align-items: center;
      margin-bottom: 24px; flex-wrap: wrap; gap: 12px;
    }
    .breadcrumb { display: flex; align-items: center; gap: 4px; }
    .back-link {
      display: flex; align-items: center; gap: 4px;
      color: #1565c0; cursor: pointer; font-weight: 500;
    }
    .back-link:hover { text-decoration: underline; }
    .separator { color: rgba(0,0,0,0.38); font-size: 18px; width: 18px; height: 18px; }
    .current-page { font-weight: 500; font-size: 18px; }
    .header-actions { display: flex; gap: 8px; }

    .loading-container { display: flex; justify-content: center; padding: 48px; }

    .kpi-grid {
      display: grid; grid-template-columns: repeat(4, 1fr);
      gap: 16px; margin-bottom: 24px;
    }
    .kpi-card mat-card-content {
      display: flex; align-items: center; gap: 16px; padding: 16px !important;
    }
    .kpi-icon {
      width: 48px; height: 48px; border-radius: 12px;
      display: flex; align-items: center; justify-content: center;
    }
    .kpi-icon mat-icon { font-size: 28px; width: 28px; height: 28px; }
    .kpi-card.repos-card .kpi-icon { background: #e3f2fd; color: #1565c0; }
    .kpi-card.packages-card .kpi-icon { background: #f3e5f5; color: #7b1fa2; }
    .kpi-card.vulns-card .kpi-icon { background: #ffebee; color: #c62828; }
    .kpi-data { display: flex; flex-direction: column; }
    .kpi-value { font-size: 28px; font-weight: 600; line-height: 1.2; }
    .kpi-label { font-size: 13px; color: rgba(0,0,0,0.54); }

    .severity-breakdown {
      display: flex; flex-wrap: wrap; gap: 12px; padding: 8px;
    }
    .sev-item { font-size: 14px; }
    .sev-item.critical { color: #c62828; }
    .sev-item.high { color: #e65100; }
    .sev-item.medium { color: #f9a825; }
    .sev-item.low { color: #2e7d32; }

    .section-card { margin-bottom: 24px; }
    .section-card mat-card-title { display: flex; align-items: center; gap: 8px; }

    .ecosystem-chips { display: flex; gap: 12px; flex-wrap: wrap; padding: 8px 0; }
    .eco-chip {
      display: flex; flex-direction: column; gap: 2px;
      padding: 10px 16px; background: #f5f5f5; border-radius: 8px;
      font-size: 13px; min-width: 140px;
    }
    .eco-vuln { color: #c62828; font-weight: 500; }

    .section-title {
      display: flex; align-items: center; gap: 8px;
      margin: 24px 0 16px; font-weight: 500;
    }

    .repo-icon { color: #1565c0; margin-right: 4px; }
    .vuln-count { color: #c62828; font-weight: 500; }

    .full-width { width: 100%; }
    .report-table { border: 1px solid rgba(0,0,0,0.08); border-radius: 8px; margin-bottom: 16px; }

    .cve-link { color: #1565c0; font-weight: 500; cursor: pointer; }
    .cve-link:hover { text-decoration: underline; }

    .severity-chip {
      display: inline-block; padding: 2px 10px; border-radius: 12px;
      font-size: 11px; font-weight: 600; text-transform: uppercase;
    }
    .severity-chip.critical { background: #ffebee; color: #c62828; }
    .severity-chip.high { background: #fff3e0; color: #e65100; }
    .severity-chip.medium { background: #fff8e1; color: #f9a825; }
    .severity-chip.low { background: #e8f5e9; color: #2e7d32; }

    .status-badge {
      padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: 500;
    }
    .status-badge.open, .status-badge.detected { background: #ffebee; color: #c62828; }
    .status-badge.resolved, .status-badge.fixed { background: #e8f5e9; color: #2e7d32; }
    .status-badge.suppressed { background: #eceff1; color: #455a64; }

    .clean-repo {
      display: flex; align-items: center; gap: 8px;
      color: #2e7d32; padding: 12px 0;
    }

    .error-card { text-align: center; color: #c62828; }
    .error-card mat-icon { margin-right: 8px; vertical-align: middle; }

    @media (max-width: 1200px) {
      .kpi-grid { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 768px) {
      .kpi-grid { grid-template-columns: 1fr; }
    }
  `,
})
export class ProjectDetailComponent implements OnInit {
  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly report = signal<ProjectDetailReport | null>(null);

  readonly vulnColumns = [
    'cveId', 'severity', 'packageName', 'installedVersion',
    'fixedVersion', 'cvssScore', 'ageDays', 'status',
  ];

  projectId = '';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly apiService: ApiService,
  ) {}

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('projectId') ?? '';
    this.loadReport();
  }

  goBack(): void {
    this.router.navigate(['/reports']);
  }

  openVuln(cveId: string): void {
    this.router.navigate(['/reports/vulnerabilities', cveId]);
  }

  exportPackagesCsv(): void {
    this.apiService.exportProjectCsv(this.projectId).subscribe(blob => {
      this.downloadBlob(blob, `${this.report()?.projectName ?? 'project'}-packages.csv`);
    });
  }

  exportVulnsCsv(): void {
    this.apiService.exportProjectVulnerabilitiesCsv(this.projectId).subscribe(blob => {
      this.downloadBlob(blob, `${this.report()?.projectName ?? 'project'}-vulnerabilities.csv`);
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
