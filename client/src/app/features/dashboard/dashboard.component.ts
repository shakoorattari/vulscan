import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { ApiService } from '../../core/services/api.service';
import { DashboardSummary, RecentScan, TopVulnerableRepo } from '../../core/models/api.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatTableModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatDividerModule,
  ],
  template: `
    <div class="dashboard">
      <div class="page-header">
        <h2>Executive Dashboard</h2>
        <button mat-flat-button color="primary" (click)="refresh()">
          <mat-icon>refresh</mat-icon>
          Refresh
        </button>
      </div>

      @if (loading()) {
        <div class="loading-container">
          <mat-spinner diameter="48"></mat-spinner>
        </div>
      } @else if (summary()) {
        <!-- KPI Cards -->
        <div class="kpi-grid">
          <mat-card class="kpi-card critical">
            <mat-card-content>
              <div class="kpi-icon">
                <mat-icon>error</mat-icon>
              </div>
              <div class="kpi-data">
                <span class="kpi-value">{{ summary()!.criticalCount }}</span>
                <span class="kpi-label">Critical</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card high">
            <mat-card-content>
              <div class="kpi-icon">
                <mat-icon>warning</mat-icon>
              </div>
              <div class="kpi-data">
                <span class="kpi-value">{{ summary()!.highCount }}</span>
                <span class="kpi-label">High</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card medium">
            <mat-card-content>
              <div class="kpi-icon">
                <mat-icon>info</mat-icon>
              </div>
              <div class="kpi-data">
                <span class="kpi-value">{{ summary()!.mediumCount }}</span>
                <span class="kpi-label">Medium</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card low">
            <mat-card-content>
              <div class="kpi-icon">
                <mat-icon>check_circle</mat-icon>
              </div>
              <div class="kpi-data">
                <span class="kpi-value">{{ summary()!.lowCount }}</span>
                <span class="kpi-label">Low</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card total">
            <mat-card-content>
              <div class="kpi-icon">
                <mat-icon>bug_report</mat-icon>
              </div>
              <div class="kpi-data">
                <span class="kpi-value">{{ summary()!.totalVulnerabilities }}</span>
                <span class="kpi-label">Total Vulnerabilities</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card repos">
            <mat-card-content>
              <div class="kpi-icon">
                <mat-icon>folder</mat-icon>
              </div>
              <div class="kpi-data">
                <span class="kpi-value">{{ summary()!.totalRepositories }}</span>
                <span class="kpi-label">Repositories</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card scans">
            <mat-card-content>
              <div class="kpi-icon">
                <mat-icon>radar</mat-icon>
              </div>
              <div class="kpi-data">
                <span class="kpi-value">{{ summary()!.totalScans }}</span>
                <span class="kpi-label">Total Scans</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card last-scan">
            <mat-card-content>
              <div class="kpi-icon">
                <mat-icon>schedule</mat-icon>
              </div>
              <div class="kpi-data">
                <span class="kpi-value status" [class]="summary()!.lastScanStatus?.toLowerCase() ?? ''">
                  {{ summary()!.lastScanStatus ?? 'N/A' }}
                </span>
                <span class="kpi-label">Last Scan</span>
              </div>
            </mat-card-content>
          </mat-card>
        </div>

        <!-- Recent Scans -->
        <div class="tables-grid">
          <mat-card>
            <mat-card-header>
              <mat-card-title>Recent Scans</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              @if (summary()!.recentScans.length > 0) {
                <table mat-table [dataSource]="summary()!.recentScans" class="full-width">
                  <ng-container matColumnDef="id">
                    <th mat-header-cell *matHeaderCellDef>ID</th>
                    <td mat-cell *matCellDef="let scan">#{{ scan.id }}</td>
                  </ng-container>
                  <ng-container matColumnDef="startedAt">
                    <th mat-header-cell *matHeaderCellDef>Started</th>
                    <td mat-cell *matCellDef="let scan">{{ scan.startedAt | date : 'short' }}</td>
                  </ng-container>
                  <ng-container matColumnDef="status">
                    <th mat-header-cell *matHeaderCellDef>Status</th>
                    <td mat-cell *matCellDef="let scan">
                      <span class="status-badge" [class]="scan.status.toLowerCase()">
                        {{ scan.status }}
                      </span>
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="repos">
                    <th mat-header-cell *matHeaderCellDef>Repos</th>
                    <td mat-cell *matCellDef="let scan">{{ scan.reposScanned }}</td>
                  </ng-container>
                  <ng-container matColumnDef="vulns">
                    <th mat-header-cell *matHeaderCellDef>Vulns</th>
                    <td mat-cell *matCellDef="let scan">{{ scan.totalVulnerabilities }}</td>
                  </ng-container>
                  <ng-container matColumnDef="triggeredBy">
                    <th mat-header-cell *matHeaderCellDef>By</th>
                    <td mat-cell *matCellDef="let scan">{{ scan.triggeredBy }}</td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="recentScanColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: recentScanColumns"></tr>
                </table>
              } @else {
                <p class="no-data">No scans recorded yet.</p>
              }
            </mat-card-content>
          </mat-card>

          <mat-card>
            <mat-card-header>
              <mat-card-title>Top Vulnerable Repositories</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              @if (summary()!.topVulnerableRepos.length > 0) {
                <table mat-table [dataSource]="summary()!.topVulnerableRepos" class="full-width">
                  <ng-container matColumnDef="repository">
                    <th mat-header-cell *matHeaderCellDef>Repository</th>
                    <td mat-cell *matCellDef="let repo">
                      <strong>{{ repo.repositoryName }}</strong>
                      <br />
                      <small>{{ repo.projectName }}</small>
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="critical">
                    <th mat-header-cell *matHeaderCellDef>Critical</th>
                    <td mat-cell *matCellDef="let repo">
                      <span class="severity critical-text">{{ repo.criticalCount }}</span>
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="high">
                    <th mat-header-cell *matHeaderCellDef>High</th>
                    <td mat-cell *matCellDef="let repo">
                      <span class="severity high-text">{{ repo.highCount }}</span>
                    </td>
                  </ng-container>
                  <ng-container matColumnDef="total">
                    <th mat-header-cell *matHeaderCellDef>Total</th>
                    <td mat-cell *matCellDef="let repo">{{ repo.totalVulnerabilities }}</td>
                  </ng-container>
                  <tr mat-header-row *matHeaderRowDef="topRepoColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: topRepoColumns"></tr>
                </table>
              } @else {
                <p class="no-data">No vulnerability data available.</p>
              }
            </mat-card-content>
          </mat-card>
        </div>
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
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }

    .page-header h2 {
      margin: 0;
      font-weight: 500;
    }

    .loading-container {
      display: flex;
      justify-content: center;
      padding: 48px;
    }

    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
      margin-bottom: 24px;
    }

    .kpi-card mat-card-content {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 16px !important;
    }

    .kpi-icon {
      width: 48px;
      height: 48px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .kpi-icon mat-icon {
      font-size: 28px;
      width: 28px;
      height: 28px;
    }

    .kpi-card.critical .kpi-icon {
      background: var(--sev-critical-bg);
      color: var(--sev-critical);
    }
    .kpi-card.high .kpi-icon {
      background: var(--sev-high-bg);
      color: var(--sev-high);
    }
    .kpi-card.medium .kpi-icon {
      background: var(--sev-medium-bg);
      color: var(--sev-medium);
    }
    .kpi-card.low .kpi-icon {
      background: var(--sev-low-bg);
      color: var(--sev-low);
    }
    .kpi-card.total .kpi-icon {
      background: var(--brand-teal-50);
      color: var(--brand-teal);
    }
    .kpi-card.repos .kpi-icon {
      background: var(--brand-peach-50);
      color: var(--brand-rust);
    }
    .kpi-card.scans .kpi-icon {
      background: var(--brand-navy-50);
      color: var(--brand-navy);
    }
    .kpi-card.last-scan .kpi-icon {
      background: var(--neutral-200);
      color: var(--neutral-700);
    }

    .kpi-data {
      display: flex;
      flex-direction: column;
    }

    .kpi-value {
      font-size: 28px;
      font-weight: 600;
      line-height: 1.2;
    }

    .kpi-label {
      font-size: 13px;
      color: rgba(0, 0, 0, 0.54);
    }

    .tables-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }

    .full-width {
      width: 100%;
    }

    .status-badge {
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 12px;
      font-weight: 500;
    }

    .status-badge.completed {
      background: var(--status-success-bg);
      color: var(--status-success);
    }
    .status-badge.running {
      background: var(--status-info-bg);
      color: var(--status-info);
    }
    .status-badge.queued {
      background: var(--status-warn-bg);
      color: var(--status-warn);
    }
    .status-badge.failed {
      background: var(--status-error-bg);
      color: var(--status-error);
    }

    .severity.critical-text {
      color: var(--sev-critical);
      font-weight: 600;
    }
    .severity.high-text {
      color: var(--sev-high);
      font-weight: 600;
    }

    .status.completed {
      color: var(--status-success);
    }
    .status.running {
      color: var(--status-info);
    }
    .status.failed {
      color: var(--status-error);
    }

    .no-data {
      text-align: center;
      padding: 24px;
      color: rgba(0, 0, 0, 0.38);
    }

    .error-card {
      text-align: center;
      color: #c62828;
    }

    .error-card mat-icon {
      margin-right: 8px;
      vertical-align: middle;
    }

    @media (max-width: 1200px) {
      .kpi-grid {
        grid-template-columns: repeat(2, 1fr);
      }
      .tables-grid {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class DashboardComponent implements OnInit {
  readonly summary = signal<DashboardSummary | null>(null);
  readonly loading = signal(true);
  readonly errorMessage = signal('');

  readonly recentScanColumns = ['id', 'startedAt', 'status', 'repos', 'vulns', 'triggeredBy'];
  readonly topRepoColumns = ['repository', 'critical', 'high', 'total'];

  constructor(
    private readonly apiService: ApiService,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.loadSummary();
  }

  refresh(): void {
    this.loadSummary();
  }

  private loadSummary(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.apiService.getDashboardSummary().subscribe({
      next: (response) => {
        this.loading.set(false);
        if (response.success && response.data) {
          this.summary.set(response.data);
        } else {
          this.errorMessage.set(response.message ?? 'Failed to load dashboard data.');
        }
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set('Failed to connect to the API server.');
      },
    });
  }
}
