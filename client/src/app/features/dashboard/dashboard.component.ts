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
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
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
