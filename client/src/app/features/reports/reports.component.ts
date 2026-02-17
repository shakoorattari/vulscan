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
  template: `
    <div class="reports">
      <div class="page-header">
        <h2>Reports</h2>
        <div class="header-actions">
          <button mat-flat-button color="primary" (click)="refresh()">
            <mat-icon>refresh</mat-icon>
            Refresh
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="loading-container">
          <mat-spinner diameter="48"></mat-spinner>
        </div>
      } @else if (errorMessage()) {
        <mat-card class="error-card">
          <mat-card-content>
            <mat-icon>error_outline</mat-icon>
            <span>{{ errorMessage() }}</span>
          </mat-card-content>
        </mat-card>
      } @else {
        <!-- Summary KPI cards -->
        <div class="kpi-grid">
          <mat-card class="kpi-card total-projects">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>folder_special</mat-icon></div>
              <div class="kpi-data">
                <span class="kpi-value">{{ totalProjects() }}</span>
                <span class="kpi-label">Projects</span>
              </div>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card vulnerable-projects">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>warning</mat-icon></div>
              <div class="kpi-data">
                <span class="kpi-value">{{ vulnerableProjects() }}</span>
                <span class="kpi-label">Vulnerable Projects</span>
              </div>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card unique-cves">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>bug_report</mat-icon></div>
              <div class="kpi-data">
                <span class="kpi-value">{{ uniqueCves() }}</span>
                <span class="kpi-label">Unique CVEs</span>
              </div>
            </mat-card-content>
          </mat-card>
          <mat-card class="kpi-card ecosystems">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>layers</mat-icon></div>
              <div class="kpi-data">
                <span class="kpi-value">{{ ecosystemBreakdown().length }}</span>
                <span class="kpi-label">Ecosystems</span>
              </div>
            </mat-card-content>
          </mat-card>
        </div>

        <!-- Ecosystem Breakdown -->
        @if (ecosystemBreakdown().length) {
          <mat-card class="ecosystem-card">
            <mat-card-header>
              <mat-card-title>
                <mat-icon>layers</mat-icon> Ecosystem Breakdown
              </mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="ecosystem-grid">
                @for (eco of ecosystemBreakdown(); track eco.ecosystem) {
                  <div class="ecosystem-item">
                    <div class="eco-name">{{ eco.ecosystem | uppercase }}</div>
                    <div class="eco-stats">
                      <span class="eco-stat">
                        <strong>{{ eco.totalPackages | number }}</strong> packages
                      </span>
                      <span class="eco-stat">
                        <strong>{{ eco.uniquePackages | number }}</strong> unique
                      </span>
                      <span class="eco-stat vulnerable" [class.has-vulns]="eco.vulnerablePackages > 0">
                        <strong>{{ eco.vulnerablePackages }}</strong> vulnerable
                      </span>
                    </div>
                  </div>
                }
              </div>
            </mat-card-content>
          </mat-card>
        }

        <!-- Tabbed content -->
        <mat-tab-group animationDuration="200ms" (selectedTabChange)="onTabChange($event.index)">
          <!-- Projects Tab -->
          <mat-tab>
            <ng-template mat-tab-label>
              <mat-icon>folder</mat-icon>&nbsp; Projects ({{ projects().length }})
            </ng-template>

            <div class="tab-content">
              <div class="tab-actions">
                <mat-form-field appearance="outline" class="filter-field">
                  <mat-label>Filter</mat-label>
                  <mat-select (selectionChange)="filterProjects($event.value)" value="all">
                    <mat-option value="all">All Projects</mat-option>
                    <mat-option value="vulnerable">Vulnerable Only</mat-option>
                    <mat-option value="clean">Clean Only</mat-option>
                  </mat-select>
                </mat-form-field>
              </div>

              @if (filteredProjects().length) {
                <table mat-table [dataSource]="filteredProjects()" matSort
                       (matSortChange)="sortProjects($event)" class="full-width report-table">
                  <ng-container matColumnDef="projectName">
                    <th mat-header-cell *matHeaderCellDef mat-sort-header>Project</th>
                    <td mat-cell *matCellDef="let p">
                      <a class="project-link" (click)="openProjectDetail(p.projectId)">
                        {{ p.projectName }}
                      </a>
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="repositoryCount">
                    <th mat-header-cell *matHeaderCellDef mat-sort-header>Repos</th>
                    <td mat-cell *matCellDef="let p">{{ p.repositoryCount }}</td>
                  </ng-container>

                  <ng-container matColumnDef="totalPackages">
                    <th mat-header-cell *matHeaderCellDef mat-sort-header>Packages</th>
                    <td mat-cell *matCellDef="let p">{{ p.totalPackages | number }}</td>
                  </ng-container>

                  <ng-container matColumnDef="critical">
                    <th mat-header-cell *matHeaderCellDef mat-sort-header="criticalCount">Critical</th>
                    <td mat-cell *matCellDef="let p">
                      <span class="severity-badge critical" [class.zero]="p.criticalCount === 0">
                        {{ p.criticalCount }}
                      </span>
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="high">
                    <th mat-header-cell *matHeaderCellDef mat-sort-header="highCount">High</th>
                    <td mat-cell *matCellDef="let p">
                      <span class="severity-badge high" [class.zero]="p.highCount === 0">
                        {{ p.highCount }}
                      </span>
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="medium">
                    <th mat-header-cell *matHeaderCellDef mat-sort-header="mediumCount">Medium</th>
                    <td mat-cell *matCellDef="let p">
                      <span class="severity-badge medium" [class.zero]="p.mediumCount === 0">
                        {{ p.mediumCount }}
                      </span>
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="totalVulnerabilities">
                    <th mat-header-cell *matHeaderCellDef mat-sort-header>Total</th>
                    <td mat-cell *matCellDef="let p">
                      <strong>{{ p.totalVulnerabilities }}</strong>
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="actions">
                    <th mat-header-cell *matHeaderCellDef></th>
                    <td mat-cell *matCellDef="let p">
                      <button mat-icon-button matTooltip="View Details"
                              (click)="openProjectDetail(p.projectId)">
                        <mat-icon>visibility</mat-icon>
                      </button>
                      <button mat-icon-button matTooltip="Export CSV"
                              (click)="exportProjectCsv(p.projectId, p.projectName)">
                        <mat-icon>download</mat-icon>
                      </button>
                    </td>
                  </ng-container>

                  <tr mat-header-row *matHeaderRowDef="projectColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: projectColumns"
                      [class.vulnerable-row]="row.totalVulnerabilities > 0"
                      (click)="openProjectDetail(row.projectId)"></tr>
                </table>
              } @else {
                <p class="no-data">No projects match the current filter.</p>
              }
            </div>
          </mat-tab>

          <!-- Vulnerabilities Tab -->
          <mat-tab>
            <ng-template mat-tab-label>
              <mat-icon>security</mat-icon>&nbsp; Vulnerabilities ({{ vulnerabilities().length }})
            </ng-template>

            <div class="tab-content">
              <div class="tab-actions">
                <mat-form-field appearance="outline" class="filter-field">
                  <mat-label>Severity</mat-label>
                  <mat-select (selectionChange)="filterBySeverity($event.value)" value="">
                    <mat-option value="">All Severities</mat-option>
                    <mat-option value="Critical">Critical</mat-option>
                    <mat-option value="High">High</mat-option>
                    <mat-option value="Medium">Medium</mat-option>
                    <mat-option value="Low">Low</mat-option>
                  </mat-select>
                </mat-form-field>
                <button mat-stroked-button (click)="exportAllVulnsCsv()">
                  <mat-icon>download</mat-icon> Export CSV
                </button>
              </div>

              @if (loadingVulns()) {
                <div class="loading-container">
                  <mat-spinner diameter="36"></mat-spinner>
                </div>
              } @else if (filteredVulnerabilities().length) {
                <table mat-table [dataSource]="filteredVulnerabilities()" class="full-width report-table">
                  <ng-container matColumnDef="cveId">
                    <th mat-header-cell *matHeaderCellDef>CVE</th>
                    <td mat-cell *matCellDef="let v">
                      <a class="cve-link" (click)="openVulnDetail(v.cveId); $event.stopPropagation()">
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

                  <ng-container matColumnDef="cvssScore">
                    <th mat-header-cell *matHeaderCellDef>CVSS</th>
                    <td mat-cell *matCellDef="let v">
                      {{ v.cvssScore != null ? v.cvssScore.toFixed(1) : '—' }}
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="packageName">
                    <th mat-header-cell *matHeaderCellDef>Package</th>
                    <td mat-cell *matCellDef="let v">{{ v.packageName }}</td>
                  </ng-container>

                  <ng-container matColumnDef="fixedVersion">
                    <th mat-header-cell *matHeaderCellDef>Fix Available</th>
                    <td mat-cell *matCellDef="let v">
                      @if (v.fixedVersion) {
                        <span class="fix-available">{{ v.fixedVersion }}</span>
                      } @else {
                        <span class="no-fix">No fix</span>
                      }
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="affectedRepositories">
                    <th mat-header-cell *matHeaderCellDef>Affected Repos</th>
                    <td mat-cell *matCellDef="let v">{{ v.affectedRepositories }}</td>
                  </ng-container>

                  <ng-container matColumnDef="totalOccurrences">
                    <th mat-header-cell *matHeaderCellDef>Occurrences</th>
                    <td mat-cell *matCellDef="let v">{{ v.totalOccurrences }}</td>
                  </ng-container>

                  <ng-container matColumnDef="vulnActions">
                    <th mat-header-cell *matHeaderCellDef></th>
                    <td mat-cell *matCellDef="let v">
                      <button mat-icon-button matTooltip="View Details"
                              (click)="openVulnDetail(v.cveId); $event.stopPropagation()">
                        <mat-icon>visibility</mat-icon>
                      </button>
                    </td>
                  </ng-container>

                  <tr mat-header-row *matHeaderRowDef="vulnColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: vulnColumns"
                      (click)="openVulnDetail(row.cveId)"></tr>
                </table>
              } @else {
                <p class="no-data">No vulnerabilities found.</p>
              }
            </div>
          </mat-tab>

          <!-- Trends Tab -->
          <mat-tab>
            <ng-template mat-tab-label>
              <mat-icon>trending_up</mat-icon>&nbsp; Trends
            </ng-template>

            <div class="tab-content">
              @if (trends().length) {
                <mat-card>
                  <mat-card-header>
                    <mat-card-title>Severity Trend Over Scans</mat-card-title>
                  </mat-card-header>
                  <mat-card-content>
                    <div class="trend-table-wrapper">
                      <table mat-table [dataSource]="trends()" class="full-width report-table">
                        <ng-container matColumnDef="scanDate">
                          <th mat-header-cell *matHeaderCellDef>Scan Date</th>
                          <td mat-cell *matCellDef="let t">{{ t.scanDate | date:'medium' }}</td>
                        </ng-container>
                        <ng-container matColumnDef="scanId">
                          <th mat-header-cell *matHeaderCellDef>Scan #</th>
                          <td mat-cell *matCellDef="let t">#{{ t.scanId }}</td>
                        </ng-container>
                        <ng-container matColumnDef="trendCritical">
                          <th mat-header-cell *matHeaderCellDef>Critical</th>
                          <td mat-cell *matCellDef="let t">
                            <span class="severity-badge critical" [class.zero]="t.critical === 0">{{ t.critical }}</span>
                          </td>
                        </ng-container>
                        <ng-container matColumnDef="trendHigh">
                          <th mat-header-cell *matHeaderCellDef>High</th>
                          <td mat-cell *matCellDef="let t">
                            <span class="severity-badge high" [class.zero]="t.high === 0">{{ t.high }}</span>
                          </td>
                        </ng-container>
                        <ng-container matColumnDef="trendMedium">
                          <th mat-header-cell *matHeaderCellDef>Medium</th>
                          <td mat-cell *matCellDef="let t">
                            <span class="severity-badge medium" [class.zero]="t.medium === 0">{{ t.medium }}</span>
                          </td>
                        </ng-container>
                        <ng-container matColumnDef="trendLow">
                          <th mat-header-cell *matHeaderCellDef>Low</th>
                          <td mat-cell *matCellDef="let t">
                            <span class="severity-badge low" [class.zero]="t.low === 0">{{ t.low }}</span>
                          </td>
                        </ng-container>
                        <ng-container matColumnDef="trendTotal">
                          <th mat-header-cell *matHeaderCellDef>Total</th>
                          <td mat-cell *matCellDef="let t"><strong>{{ t.total }}</strong></td>
                        </ng-container>
                        <tr mat-header-row *matHeaderRowDef="trendColumns"></tr>
                        <tr mat-row *matRowDef="let row; columns: trendColumns"></tr>
                      </table>
                    </div>

                    <!-- Simple visual bar chart -->
                    <div class="trend-chart">
                      @for (t of trends(); track t.scanId) {
                        <div class="trend-bar-group">
                          <div class="trend-bars">
                            @if (t.critical) {
                              <div class="trend-bar critical" [style.height.px]="barHeight(t.critical)"
                                   matTooltip="Critical: {{ t.critical }}"></div>
                            }
                            @if (t.high) {
                              <div class="trend-bar high" [style.height.px]="barHeight(t.high)"
                                   matTooltip="High: {{ t.high }}"></div>
                            }
                            @if (t.medium) {
                              <div class="trend-bar medium" [style.height.px]="barHeight(t.medium)"
                                   matTooltip="Medium: {{ t.medium }}"></div>
                            }
                            @if (t.low) {
                              <div class="trend-bar low" [style.height.px]="barHeight(t.low)"
                                   matTooltip="Low: {{ t.low }}"></div>
                            }
                          </div>
                          <span class="trend-label">#{{ t.scanId }}</span>
                        </div>
                      }
                    </div>
                  </mat-card-content>
                </mat-card>
              } @else {
                <p class="no-data">No trend data available yet.</p>
              }
            </div>
          </mat-tab>
        </mat-tab-group>
      }
    </div>
  `,
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

  openProjectDetail(projectId: number): void {
    this.router.navigate(['/reports/projects', projectId]);
  }

  openVulnDetail(cveId: string): void {
    this.router.navigate(['/reports/vulnerabilities', cveId]);
  }

  exportProjectCsv(projectId: number, projectName: string): void {
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
