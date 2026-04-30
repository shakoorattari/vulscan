import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabGroup, MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import {
  CreateInstanceRequest,
  InstanceDto,
  PagedResult,
  ScanRun,
} from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-scans',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatTableModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSnackBarModule,
    MatDividerModule,
    MatTabsModule,
    MatTooltipModule,
  ],
  template: `
    <div class="scans-page">
      <!-- Hero header -->
      <header class="page-hero">
        <div class="hero-text">
          <h1>Scan Management</h1>
          <p class="hero-subtitle">
            Configure projects, run vulnerability scans, and review historical results.
          </p>
        </div>
        <div class="hero-actions">
          <button mat-flat-button color="primary" (click)="showAddInstance.set(true)">
            <mat-icon>add</mat-icon>
            Add Project
          </button>
        </div>
      </header>

      <mat-tab-group #tabGroup [(selectedIndex)]="selectedTab" animationDuration="200ms">
        <!-- Projects Tab -->
        <mat-tab label="Projects">
          <div class="tab-content">
            @if (showAddInstance()) {
              <mat-card class="form-card elevated">
                <mat-card-header>
                  <mat-card-title>Add Azure DevOps Project</mat-card-title>
                  <mat-card-subtitle>
                    Connect a new repository source for SBOM and CVE scanning
                  </mat-card-subtitle>
                </mat-card-header>
                <mat-card-content>
                  <div class="form-grid">
                    <mat-form-field appearance="outline" class="full-width">
                      <mat-label>Project Name</mat-label>
                      <input
                        matInput
                        [(ngModel)]="newInstance.name"
                        placeholder="e.g., TransLynk Production"
                      />
                      <mat-hint>A friendly name for this project</mat-hint>
                    </mat-form-field>

                    <mat-form-field appearance="outline" class="full-width">
                      <mat-label>Azure DevOps Project URL</mat-label>
                      <input
                        matInput
                        [(ngModel)]="newInstance.projectUrl"
                        placeholder="https://devops.ishj.ae/SDD/TransLynk"
                      />
                      <mat-hint>Full URL to your Azure DevOps project</mat-hint>
                    </mat-form-field>

                    <mat-form-field appearance="outline">
                      <mat-label>Username</mat-label>
                      <input
                        matInput
                        [(ngModel)]="newInstance.username"
                        placeholder="user@domain.com"
                      />
                    </mat-form-field>

                    <mat-form-field appearance="outline">
                      <mat-label>Password / PAT</mat-label>
                      <input
                        matInput
                        type="password"
                        [(ngModel)]="newInstance.password"
                        placeholder="Enter password or PAT"
                      />
                      <mat-hint>Personal Access Token recommended</mat-hint>
                    </mat-form-field>

                    <mat-form-field appearance="outline">
                      <mat-label>Default Branch</mat-label>
                      <input matInput [(ngModel)]="newInstance.branch" placeholder="main" />
                      <mat-hint>Branch to scan (default: main)</mat-hint>
                    </mat-form-field>
                  </div>

                  <div class="form-actions">
                    <button
                      mat-flat-button
                      color="primary"
                      (click)="addInstance()"
                      [disabled]="addingInstance()"
                    >
                      @if (addingInstance()) {
                        <mat-spinner diameter="18"></mat-spinner>
                      } @else {
                        <ng-container>
                          <mat-icon>save</mat-icon>
                          Save Project
                        </ng-container>
                      }
                    </button>
                    <button mat-stroked-button (click)="cancelAddInstance()">Cancel</button>
                  </div>
                </mat-card-content>
              </mat-card>
            }

            <mat-card class="elevated">
              <mat-card-header>
                <mat-card-title>Configured Projects</mat-card-title>
                <mat-card-subtitle>
                  {{ instances().length }}
                  {{ instances().length === 1 ? 'project' : 'projects' }} configured
                </mat-card-subtitle>
                <span class="spacer"></span>
                <button mat-icon-button (click)="loadInstances()" matTooltip="Refresh">
                  <mat-icon>refresh</mat-icon>
                </button>
              </mat-card-header>
              <mat-card-content>
                @if (loadingInstances()) {
                  <div class="loading-container">
                    <mat-spinner diameter="36"></mat-spinner>
                  </div>
                } @else if (instances().length > 0) {
                  <div class="table-wrapper">
                    <table mat-table [dataSource]="instances()" class="modern-table">
                      <ng-container matColumnDef="name">
                        <th mat-header-cell *matHeaderCellDef>Project</th>
                        <td mat-cell *matCellDef="let inst">
                          <div class="project-cell">
                            <div class="project-avatar">
                              {{ initials(inst.name) }}
                            </div>
                            <div class="project-info">
                              <strong>{{ inst.name }}</strong>
                              <small class="text-muted">{{ inst.projectName }}</small>
                              <small class="url-text">{{ inst.url }}/{{ inst.collection }}</small>
                            </div>
                          </div>
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="lastScan">
                        <th mat-header-cell *matHeaderCellDef>Last Scan</th>
                        <td mat-cell *matCellDef="let inst">
                          @if (inst.lastScannedAt) {
                            <div class="last-scan-cell">
                              <div class="last-scan-meta">
                                <span class="status-badge" [class]="(inst.lastScanStatus ?? '').toLowerCase()">
                                  {{ inst.lastScanStatus ?? 'Unknown' }}
                                </span>
                                <span class="text-muted timestamp" [matTooltip]="inst.lastScannedAt | date : 'medium'">
                                  {{ inst.lastScannedAt | date : 'MMM d, y, h:mm a' }}
                                </span>
                              </div>
                              <div class="vuln-summary">
                                @if (inst.lastScanCriticalCount > 0) {
                                  <span class="severity-count critical" matTooltip="Critical">
                                    {{ inst.lastScanCriticalCount }}C
                                  </span>
                                }
                                @if (inst.lastScanHighCount > 0) {
                                  <span class="severity-count high" matTooltip="High">
                                    {{ inst.lastScanHighCount }}H
                                  </span>
                                }
                                @if (inst.lastScanMediumCount > 0) {
                                  <span class="severity-count medium" matTooltip="Medium">
                                    {{ inst.lastScanMediumCount }}M
                                  </span>
                                }
                                @if (inst.lastScanLowCount > 0) {
                                  <span class="severity-count low" matTooltip="Low">
                                    {{ inst.lastScanLowCount }}L
                                  </span>
                                }
                                @if (inst.lastScanTotalVulnerabilities === 0) {
                                  <span class="no-vulns">
                                    <mat-icon class="check-icon">check_circle</mat-icon>
                                    Clean
                                  </span>
                                }
                              </div>
                            </div>
                          } @else {
                            <span class="text-muted">Never scanned</span>
                          }
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="totals">
                        <th mat-header-cell *matHeaderCellDef>Totals</th>
                        <td mat-cell *matCellDef="let inst">
                          <div class="totals-cell">
                            <span class="stat-pill">
                              <mat-icon>history</mat-icon>
                              {{ inst.totalScans }} scans
                            </span>
                            <span class="stat-pill">
                              <mat-icon>bug_report</mat-icon>
                              {{ inst.totalVulnerabilities }} vulns
                            </span>
                          </div>
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="status">
                        <th mat-header-cell *matHeaderCellDef>Status</th>
                        <td mat-cell *matCellDef="let inst">
                          <span
                            class="status-badge"
                            [class.enabled]="inst.isEnabled"
                            [class.disabled]="!inst.isEnabled"
                          >
                            <span class="status-dot"></span>
                            {{ inst.isEnabled ? 'Active' : 'Disabled' }}
                          </span>
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="actions">
                        <th mat-header-cell *matHeaderCellDef class="actions-col">Actions</th>
                        <td mat-cell *matCellDef="let inst" class="actions-col">
                          <button
                            mat-stroked-button
                            color="primary"
                            (click)="viewScansForInstance(inst)"
                            matTooltip="View scan history for this project"
                          >
                            <mat-icon>visibility</mat-icon>
                            View Scans
                          </button>
                          <button
                            mat-flat-button
                            color="primary"
                            (click)="triggerScanForInstance(inst.id)"
                            [disabled]="triggering()"
                            matTooltip="Start vulnerability scan"
                          >
                            <mat-icon>play_arrow</mat-icon>
                            Scan
                          </button>
                          <button
                            mat-icon-button
                            color="warn"
                            (click)="deleteInstance(inst.id)"
                            matTooltip="Delete project"
                          >
                            <mat-icon>delete</mat-icon>
                          </button>
                        </td>
                      </ng-container>

                      <tr mat-header-row *matHeaderRowDef="instanceColumns"></tr>
                      <tr mat-row *matRowDef="let row; columns: instanceColumns" class="data-row"></tr>
                    </table>
                  </div>
                } @else {
                  <div class="empty-state">
                    <div class="empty-illustration">
                      <mat-icon>folder_off</mat-icon>
                    </div>
                    <h3>No projects configured yet</h3>
                    <p>Add an Azure DevOps project to start scanning for vulnerabilities.</p>
                    <button mat-flat-button color="primary" (click)="showAddInstance.set(true)">
                      <mat-icon>add</mat-icon>
                      Add Your First Project
                    </button>
                  </div>
                }
              </mat-card-content>
            </mat-card>
          </div>
        </mat-tab>

        <!-- Scan History Tab -->
        <mat-tab label="Scan History">
          <div class="tab-content">
            <mat-card class="elevated">
              <mat-card-header>
                <mat-card-title>Scan History</mat-card-title>
                <mat-card-subtitle>
                  @if (filteredInstance(); as fi) {
                    Filtered by <strong>{{ fi.name }}</strong>
                  } @else {
                    All projects
                  }
                </mat-card-subtitle>
                <span class="spacer"></span>
                @if (filteredInstance()) {
                  <button mat-stroked-button (click)="clearInstanceFilter()" class="filter-clear">
                    <mat-icon>filter_alt_off</mat-icon>
                    Clear filter
                  </button>
                }
                <button mat-icon-button (click)="loadScans()" matTooltip="Refresh">
                  <mat-icon>refresh</mat-icon>
                </button>
              </mat-card-header>
              <mat-card-content>
                @if (loading()) {
                  <div class="loading-container">
                    <mat-spinner diameter="36"></mat-spinner>
                  </div>
                } @else if (scans()?.items?.length) {
                  <div class="table-wrapper">
                    <table mat-table [dataSource]="scans()!.items" class="modern-table">
                      <ng-container matColumnDef="id">
                        <th mat-header-cell *matHeaderCellDef>ID</th>
                        <td mat-cell *matCellDef="let scan">
                          <span class="scan-id">#{{ scan.id }}</span>
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="instance">
                        <th mat-header-cell *matHeaderCellDef>Project</th>
                        <td mat-cell *matCellDef="let scan">
                          {{ scan.instanceName ?? 'N/A' }}
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="startedAt">
                        <th mat-header-cell *matHeaderCellDef>Started</th>
                        <td mat-cell *matCellDef="let scan">
                          {{ scan.startedAt | date : 'medium' }}
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="duration">
                        <th mat-header-cell *matHeaderCellDef>Duration</th>
                        <td mat-cell *matCellDef="let scan">
                          {{ scan.durationSeconds > 0 ? scan.durationSeconds + 's' : '—' }}
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="status">
                        <th mat-header-cell *matHeaderCellDef>Status</th>
                        <td mat-cell *matCellDef="let scan">
                          <span class="status-badge" [class]="scan.status.toLowerCase()">
                            <span class="status-dot"></span>
                            {{ scan.status }}
                          </span>
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="vulnerabilities">
                        <th mat-header-cell *matHeaderCellDef>Vulnerabilities</th>
                        <td mat-cell *matCellDef="let scan">
                          <div class="vuln-summary">
                            @if (scan.criticalCount > 0) {
                              <span class="severity-count critical">{{ scan.criticalCount }}C</span>
                            }
                            @if (scan.highCount > 0) {
                              <span class="severity-count high">{{ scan.highCount }}H</span>
                            }
                            @if (scan.mediumCount > 0) {
                              <span class="severity-count medium">{{ scan.mediumCount }}M</span>
                            }
                            @if (scan.lowCount > 0) {
                              <span class="severity-count low">{{ scan.lowCount }}L</span>
                            }
                            @if (scan.totalVulnerabilities === 0) {
                              <span class="no-vulns">
                                <mat-icon class="check-icon">check_circle</mat-icon>
                                None
                              </span>
                            }
                          </div>
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="triggeredBy">
                        <th mat-header-cell *matHeaderCellDef>Triggered By</th>
                        <td mat-cell *matCellDef="let scan">
                          {{ scan.triggeredBy ?? 'System' }}
                        </td>
                      </ng-container>

                      <ng-container matColumnDef="report">
                        <th mat-header-cell *matHeaderCellDef class="actions-col">Report</th>
                        <td mat-cell *matCellDef="let scan" class="actions-col">
                          <a
                            mat-stroked-button
                            color="primary"
                            [routerLink]="['/scans', scan.id, 'report']"
                            [queryParams]="filteredInstance() ? { instanceId: filteredInstance()!.id } : {}"
                            matTooltip="View detailed report with PDF/CSV export"
                          >
                            <mat-icon>description</mat-icon>
                            View Report
                          </a>
                        </td>
                      </ng-container>

                      <tr mat-header-row *matHeaderRowDef="scanColumns"></tr>
                      <tr mat-row *matRowDef="let row; columns: scanColumns" class="data-row"></tr>
                    </table>
                  </div>

                  <mat-paginator
                    [length]="scans()!.totalCount"
                    [pageSize]="pageSize"
                    [pageIndex]="currentPage - 1"
                    [pageSizeOptions]="[10, 25, 50]"
                    (page)="onPageChange($event)"
                    showFirstLastButtons
                  >
                  </mat-paginator>
                } @else {
                  <div class="empty-state">
                    <div class="empty-illustration">
                      <mat-icon>history</mat-icon>
                    </div>
                    <h3>No scan history</h3>
                    <p>
                      @if (filteredInstance()) {
                        No scans yet for this project. Trigger one from the Projects tab.
                      } @else {
                        Trigger a scan from the Projects tab to see results here.
                      }
                    </p>
                  </div>
                }
              </mat-card-content>
            </mat-card>
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: `
    :host {
      display: block;
      --gradient-primary: var(--gradient-brand);
      --shadow-card: var(--shadow-sm);
      --shadow-card-hover: var(--shadow-md);
      --radius-lg: 16px;
    }

    .scans-page {
      padding: 8px 4px 32px;
    }

    /* Hero header ----------------------------------------------------- */
    .page-hero {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 24px;
      padding: 24px 28px;
      margin-bottom: 24px;
      background: var(--gradient-primary);
      color: #fff;
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-card);
    }

    .hero-text h1 {
      margin: 0 0 4px;
      font-size: 26px;
      font-weight: 600;
      letter-spacing: -0.01em;
    }

    .hero-subtitle {
      margin: 0;
      opacity: 0.9;
      font-size: 14px;
    }

    .hero-actions button {
      background: rgba(255, 255, 255, 0.95);
      color: var(--brand-teal-700);
    }

    /* Cards & layout -------------------------------------------------- */
    .tab-content {
      padding: 20px 0 0;
    }

    .elevated {
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-card);
      transition: box-shadow 0.2s ease;
    }

    .elevated:hover {
      box-shadow: var(--shadow-card-hover);
    }

    .form-card {
      margin-bottom: 20px;
    }

    .form-grid {
      display: grid;
      grid-template-columns: repeat(2, 1fr);
      gap: 16px;
      padding-top: 8px;
    }

    .form-grid .full-width {
      grid-column: 1 / -1;
    }

    .form-actions {
      display: flex;
      gap: 12px;
      margin-top: 16px;
    }

    .spacer {
      flex: 1;
    }

    .full-width {
      width: 100%;
    }

    .filter-clear {
      margin-right: 4px;
    }

    /* Loading & empty states ----------------------------------------- */
    .loading-container {
      display: flex;
      justify-content: center;
      padding: 48px;
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 56px 24px;
    }

    .empty-illustration {
      width: 80px;
      height: 80px;
      border-radius: 50%;
      background: var(--brand-teal-50);
      display: flex;
      align-items: center;
      justify-content: center;
      margin-bottom: 20px;
    }

    .empty-illustration mat-icon {
      font-size: 40px;
      width: 40px;
      height: 40px;
      color: var(--brand-teal);
    }

    .empty-state h3 {
      margin: 0 0 8px;
      font-weight: 500;
    }

    .empty-state p {
      margin: 0 0 20px;
      color: rgba(0, 0, 0, 0.6);
      max-width: 360px;
    }

    /* Table ----------------------------------------------------------- */
    .table-wrapper {
      overflow-x: auto;
      margin: 4px -8px;
      padding: 0 8px;
    }

    .modern-table {
      width: 100%;
      background: transparent;
    }

    .modern-table th.mat-mdc-header-cell {
      font-weight: 600;
      color: rgba(0, 0, 0, 0.7);
      letter-spacing: 0.02em;
      font-size: 12px;
      text-transform: uppercase;
      background: rgba(0, 0, 0, 0.02);
    }

    .modern-table td.mat-mdc-cell,
    .modern-table th.mat-mdc-header-cell {
      padding: 14px 16px;
      border-bottom: 1px solid rgba(0, 0, 0, 0.06);
    }

    .data-row {
      transition: background 0.15s ease;
    }

    .data-row:hover {
      background: var(--brand-teal-50);
    }

    .actions-col {
      text-align: right;
      white-space: nowrap;
    }

    .actions-col button {
      margin-left: 6px;
    }

    /* Project cell --------------------------------------------------- */
    .project-cell {
      display: flex;
      align-items: center;
      gap: 12px;
    }

    .project-avatar {
      width: 40px;
      height: 40px;
      border-radius: 10px;
      background: var(--gradient-primary);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 14px;
      flex-shrink: 0;
    }

    .project-info {
      display: flex;
      flex-direction: column;
      line-height: 1.3;
    }

    .project-info strong {
      font-size: 14px;
    }

    .project-info small {
      font-size: 12px;
    }

    .text-muted {
      color: rgba(0, 0, 0, 0.55);
    }

    .url-text {
      font-family: 'SF Mono', Menlo, Consolas, monospace;
      font-size: 11px;
      color: rgba(0, 0, 0, 0.45);
    }

    /* Last-scan cell ------------------------------------------------- */
    .last-scan-cell {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .last-scan-meta {
      display: flex;
      align-items: center;
      gap: 10px;
      flex-wrap: wrap;
    }

    .timestamp {
      font-size: 12px;
    }

    /* Totals ---------------------------------------------------------- */
    .totals-cell {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }

    .stat-pill {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 4px 10px;
      background: rgba(0, 0, 0, 0.04);
      border-radius: 999px;
      font-size: 12px;
      font-weight: 500;
      color: rgba(0, 0, 0, 0.75);
    }

    .stat-pill mat-icon {
      font-size: 14px;
      width: 14px;
      height: 14px;
    }

    /* Status badges -------------------------------------------------- */
    .status-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      border-radius: 999px;
      font-size: 11px;
      font-weight: 600;
      text-transform: capitalize;
      line-height: 1;
    }

    .status-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
      opacity: 0.85;
    }

    .status-badge.enabled,
    .status-badge.completed {
      background: var(--status-success-bg);
      color: var(--status-success);
    }
    .status-badge.disabled,
    .status-badge.failed {
      background: var(--status-error-bg);
      color: var(--status-error);
    }
    .status-badge.running {
      background: var(--status-info-bg);
      color: var(--status-info);
    }
    .status-badge.queued,
    .status-badge.pending {
      background: var(--status-warn-bg);
      color: var(--status-warn);
    }

    /* Severity counts ------------------------------------------------ */
    .vuln-summary {
      display: flex;
      gap: 4px;
      flex-wrap: wrap;
      align-items: center;
    }

    .severity-count {
      padding: 2px 8px;
      border-radius: 6px;
      font-size: 11px;
      font-weight: 700;
      letter-spacing: 0.02em;
    }

    .severity-count.critical {
      background: var(--sev-critical-bg);
      color: var(--sev-critical);
    }
    .severity-count.high {
      background: var(--sev-high-bg);
      color: var(--sev-high);
    }
    .severity-count.medium {
      background: var(--sev-medium-bg);
      color: var(--sev-medium);
    }
    .severity-count.low {
      background: var(--sev-low-bg);
      color: var(--sev-low);
    }

    .no-vulns {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      color: var(--brand-teal-700);
      font-size: 12px;
      font-weight: 600;
    }

    .check-icon {
      font-size: 16px;
      width: 16px;
      height: 16px;
    }

    .scan-id {
      font-family: 'SF Mono', Menlo, Consolas, monospace;
      font-size: 12px;
      padding: 2px 8px;
      background: rgba(0, 0, 0, 0.04);
      border-radius: 6px;
      color: rgba(0, 0, 0, 0.7);
    }

    /* Responsive ------------------------------------------------------ */
    @media (max-width: 768px) {
      .page-hero {
        flex-direction: column;
        align-items: flex-start;
        padding: 20px;
      }
      .form-grid {
        grid-template-columns: 1fr;
      }
      .actions-col {
        text-align: left;
      }
      .actions-col button {
        margin-left: 0;
        margin-right: 6px;
        margin-bottom: 4px;
      }
    }
  `,
})
export class ScansComponent implements OnInit {
  @ViewChild('tabGroup') tabGroup?: MatTabGroup;

  readonly loading = signal(true);
  readonly loadingInstances = signal(true);
  readonly triggering = signal(false);
  readonly addingInstance = signal(false);
  readonly showAddInstance = signal(false);
  readonly scans = signal<PagedResult<ScanRun> | null>(null);
  readonly instances = signal<InstanceDto[]>([]);
  readonly filteredInstance = signal<InstanceDto | null>(null);

  newInstance: CreateInstanceRequest = {
    name: '',
    projectUrl: '',
    username: '',
    password: '',
    branch: 'main',
  };

  selectedTab = 0;
  currentPage = 1;
  pageSize = 25;

  readonly instanceColumns = ['name', 'lastScan', 'totals', 'status', 'actions'];
  readonly scanColumns = [
    'id',
    'instance',
    'startedAt',
    'duration',
    'status',
    'vulnerabilities',
    'triggeredBy',
    'report',
  ];

  constructor(
    private readonly apiService: ApiService,
    private readonly snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.loadInstances();
    this.loadScans();
  }

  initials(name: string): string {
    if (!name) return '?';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  loadInstances(): void {
    this.loadingInstances.set(true);
    this.apiService.getInstances().subscribe({
      next: (response) => {
        this.loadingInstances.set(false);
        if (response.success && response.data) {
          this.instances.set(response.data);
        }
      },
      error: () => {
        this.loadingInstances.set(false);
        this.snackBar.open('Failed to load projects.', 'Close', { duration: 5000 });
      },
    });
  }

  loadScans(): void {
    this.loading.set(true);
    const instanceId = this.filteredInstance()?.id;
    this.apiService.getScanHistory(this.currentPage, this.pageSize, instanceId).subscribe({
      next: (response) => {
        this.loading.set(false);
        if (response.success && response.data) {
          this.scans.set(response.data);
        }
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load scan history.', 'Close', { duration: 5000 });
      },
    });
  }

  viewScansForInstance(instance: InstanceDto): void {
    this.filteredInstance.set(instance);
    this.currentPage = 1;
    this.selectedTab = 1;
    this.loadScans();
  }

  clearInstanceFilter(): void {
    this.filteredInstance.set(null);
    this.currentPage = 1;
    this.loadScans();
  }

  addInstance(): void {
    if (
      !this.newInstance.name ||
      !this.newInstance.projectUrl ||
      !this.newInstance.username ||
      !this.newInstance.password
    ) {
      this.snackBar.open('Please fill in all required fields.', 'Close', { duration: 5000 });
      return;
    }

    this.addingInstance.set(true);
    this.apiService.createInstance(this.newInstance).subscribe({
      next: (response) => {
        this.addingInstance.set(false);
        if (response.success && response.data) {
          this.snackBar.open('Project added successfully!', 'Close', { duration: 5000 });
          this.cancelAddInstance();
          this.loadInstances();
        } else {
          this.snackBar.open(response.message ?? 'Failed to add project.', 'Close', {
            duration: 5000,
          });
        }
      },
      error: (err) => {
        this.addingInstance.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to add project.', 'Close', {
          duration: 5000,
        });
      },
    });
  }

  cancelAddInstance(): void {
    this.showAddInstance.set(false);
    this.newInstance = {
      name: '',
      projectUrl: '',
      username: '',
      password: '',
      branch: 'main',
    };
  }

  triggerScanForInstance(instanceId: string): void {
    this.triggering.set(true);
    this.apiService.triggerScan({ instanceId }).subscribe({
      next: (response) => {
        this.triggering.set(false);
        if (response.success && response.data) {
          this.snackBar.open(response.data.message, 'Close', { duration: 5000 });
          this.loadScans();
          this.loadInstances();
        } else {
          this.snackBar.open(response.message ?? 'Failed to trigger scan.', 'Close', {
            duration: 5000,
          });
        }
      },
      error: (err) => {
        this.triggering.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to trigger scan.', 'Close', {
          duration: 5000,
        });
      },
    });
  }

  deleteInstance(id: string): void {
    if (!confirm('Are you sure you want to delete this project? This action cannot be undone.')) {
      return;
    }

    this.apiService.deleteInstance(id).subscribe({
      next: (response) => {
        if (response.success) {
          this.snackBar.open('Project deleted.', 'Close', { duration: 5000 });
          // Clear filter if the filtered instance was deleted
          if (this.filteredInstance()?.id === id) {
            this.clearInstanceFilter();
          }
          this.loadInstances();
        } else {
          this.snackBar.open(response.message ?? 'Failed to delete project.', 'Close', {
            duration: 5000,
          });
        }
      },
      error: (err) => {
        this.snackBar.open(err.error?.message ?? 'Failed to delete project.', 'Close', {
          duration: 5000,
        });
      },
    });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadScans();
  }
}
