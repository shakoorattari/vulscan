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
  template: `
    <div class="project-detail">
      <div class="page-header">
        <div class="breadcrumb">
          <a class="back-link" (click)="goBack()">
            <mat-icon>arrow_back</mat-icon> Reports
          </a>
          <mat-icon class="separator">chevron_right</mat-icon>
          <span class="current-page">{{ report()?.projectName ?? 'Project Detail' }}</span>
        </div>
        <div class="header-actions">
          <button
            mat-stroked-button
            color="primary"
            [routerLink]="['/packages']"
            [queryParams]="{ projectId: projectId }"
            matTooltip="View all packages discovered in this project"
          >
            <mat-icon>inventory_2</mat-icon> View Packages
          </button>
          <button mat-stroked-button (click)="exportPackagesCsv()">
            <mat-icon>download</mat-icon> Packages CSV
          </button>
          <button mat-stroked-button (click)="exportVulnsCsv()">
            <mat-icon>download</mat-icon> Vulnerabilities CSV
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="loading-container">
          <mat-spinner diameter="48"></mat-spinner>
        </div>
      } @else if (report()) {
        <!-- Project Summary KPIs -->
        <div class="kpi-grid">
          <mat-card class="kpi-card repos-card">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>source</mat-icon></div>
              <div class="kpi-data">
                <span class="kpi-value">{{ report()!.totalRepositories }}</span>
                <span class="kpi-label">Repositories</span>
              </div>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card packages-card">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>inventory_2</mat-icon></div>
              <div class="kpi-data">
                <span class="kpi-value">{{ report()!.totalPackages | number }}</span>
                <span class="kpi-label">Packages</span>
              </div>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card vulns-card">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>bug_report</mat-icon></div>
              <div class="kpi-data">
                <span class="kpi-value">{{ report()!.totalVulnerabilities }}</span>
                <span class="kpi-label">Vulnerabilities</span>
              </div>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card severity-card">
            <mat-card-content>
              <div class="severity-breakdown">
                <span class="sev-item critical">
                  <strong>{{ report()!.criticalCount }}</strong> Critical
                </span>
                <span class="sev-item high">
                  <strong>{{ report()!.highCount }}</strong> High
                </span>
                <span class="sev-item medium">
                  <strong>{{ report()!.mediumCount }}</strong> Medium
                </span>
                <span class="sev-item low">
                  <strong>{{ report()!.lowCount }}</strong> Low
                </span>
              </div>
            </mat-card-content>
          </mat-card>
        </div>

        <!-- Ecosystem Breakdown -->
        @if (report()!.ecosystemBreakdown.length) {
          <mat-card class="section-card">
            <mat-card-header>
              <mat-card-title>
                <mat-icon>layers</mat-icon> Ecosystem Breakdown
              </mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="ecosystem-chips">
                @for (eco of report()!.ecosystemBreakdown; track eco.ecosystem) {
                  <div class="eco-chip">
                    <strong>{{ eco.ecosystem | uppercase }}</strong>
                    <span>{{ eco.totalPackages | number }} pkgs</span>
                    <span>{{ eco.uniquePackages }} unique</span>
                    @if (eco.vulnerablePackages > 0) {
                      <span class="eco-vuln">{{ eco.vulnerablePackages }} vulnerable</span>
                    }
                  </div>
                }
              </div>
            </mat-card-content>
          </mat-card>
        }

        <!-- Repositories -->
        <h3 class="section-title">
          <mat-icon>source</mat-icon> Repositories ({{ report()!.repositories.length }})
        </h3>

        <mat-accordion multi>
          @for (repo of report()!.repositories; track repo.repositoryId) {
            <mat-expansion-panel [expanded]="repo.vulnerabilities.length > 0">
              <mat-expansion-panel-header>
                <mat-panel-title>
                  <mat-icon class="repo-icon">folder</mat-icon>
                  {{ repo.repositoryName }}
                </mat-panel-title>
                <mat-panel-description>
                  {{ repo.totalPackages | number }} packages
                  @if (repo.vulnerablePackages > 0) {
                    · <span class="vuln-count">{{ repo.vulnerablePackages }} vulnerable</span>
                  }
                </mat-panel-description>
              </mat-expansion-panel-header>

              @if (repo.vulnerabilities.length) {
                <h4>Vulnerabilities ({{ repo.vulnerabilities.length }})</h4>
                <table mat-table [dataSource]="repo.vulnerabilities" class="full-width report-table">
                  <ng-container matColumnDef="cveId">
                    <th mat-header-cell *matHeaderCellDef>CVE</th>
                    <td mat-cell *matCellDef="let v">
                      <a class="cve-link" (click)="openVuln(v.cveId); $event.stopPropagation()">
                        {{ v.cveId }}
                      </a>
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="severity">
                    <th mat-header-cell *matHeaderCellDef>Severity</th>
                    <td mat-cell *matCellDef="let v">
                      <span class="severity-chip" [class]="v.severity.toLowerCase()">
                        {{ v.severity }}
                      </span>
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="packageName">
                    <th mat-header-cell *matHeaderCellDef>Package</th>
                    <td mat-cell *matCellDef="let v">{{ v.packageName }}</td>
                  </ng-container>
                  <ng-container matColumnDef="installedVersion">
                    <th mat-header-cell *matHeaderCellDef>Installed</th>
                    <td mat-cell *matCellDef="let v">{{ v.installedVersion }}</td>
                  </ng-container>
                  <ng-container matColumnDef="fixedVersion">
                    <th mat-header-cell *matHeaderCellDef>Fixed In</th>
                    <td mat-cell *matCellDef="let v">
                      {{ v.fixedVersion ?? '—' }}
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="cvssScore">
                    <th mat-header-cell *matHeaderCellDef>CVSS</th>
                    <td mat-cell *matCellDef="let v">
                      {{ v.cvssScore != null ? v.cvssScore.toFixed(1) : '—' }}
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="ageDays">
                    <th mat-header-cell *matHeaderCellDef>Age</th>
                    <td mat-cell *matCellDef="let v">{{ v.ageDays }}d</td>
                  </ng-container>
                  <ng-container matColumnDef="status">
                    <th mat-header-cell *matHeaderCellDef>Status</th>
                    <td mat-cell *matCellDef="let v">
                      <span class="status-badge" [class]="v.status.toLowerCase()">
                        {{ v.status }}
                      </span>
                    </td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="vulnColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: vulnColumns"></tr>
                </table>
              } @else {
                <p class="clean-repo">
                  <mat-icon>check_circle</mat-icon> No vulnerabilities detected in this repository.
                </p>
              }
            </mat-expansion-panel>
          }
        </mat-accordion>
      } @else if (errorMessage()) {
        <mat-card class="error-card">
          <mat-card-content>
            <mat-icon>error_outline</mat-icon>
            <span>{{ errorMessage() }}</span>
          </mat-card-content>
        </mat-card>
      }
    </div>
  `,
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
