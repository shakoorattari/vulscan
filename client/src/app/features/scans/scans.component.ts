import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
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
    MatCardModule,
    MatButtonModule,
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
      <div class="page-header">
        <h2>Scan Management</h2>
        <div class="header-actions">
          <button mat-stroked-button color="primary" (click)="showAddInstance.set(true)">
            <mat-icon>add</mat-icon>
            Add Project
          </button>
        </div>
      </div>

      <mat-tab-group>
        <!-- Projects Tab -->
        <mat-tab label="Projects">
          <div class="tab-content">
            <!-- Add New Project Card -->
            @if (showAddInstance()) {
              <mat-card class="form-card">
                <mat-card-header>
                  <mat-card-title>Add Azure DevOps Project</mat-card-title>
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

            <!-- Projects List -->
            <mat-card>
              <mat-card-header>
                <mat-card-title>Configured Projects</mat-card-title>
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
                  <table mat-table [dataSource]="instances()" class="full-width">
                    <ng-container matColumnDef="name">
                      <th mat-header-cell *matHeaderCellDef>Name</th>
                      <td mat-cell *matCellDef="let inst">
                        <strong>{{ inst.name }}</strong>
                        <br />
                        <small class="text-muted">{{ inst.projectName }}</small>
                      </td>
                    </ng-container>

                    <ng-container matColumnDef="url">
                      <th mat-header-cell *matHeaderCellDef>URL</th>
                      <td mat-cell *matCellDef="let inst">
                        <span class="url-text">{{ inst.url }}/{{ inst.collection }}</span>
                      </td>
                    </ng-container>

                    <ng-container matColumnDef="stats">
                      <th mat-header-cell *matHeaderCellDef>Stats</th>
                      <td mat-cell *matCellDef="let inst">
                        <span class="stat">{{ inst.totalScans }} scans</span>
                        <span class="stat">{{ inst.totalVulnerabilities }} vulns</span>
                      </td>
                    </ng-container>

                    <ng-container matColumnDef="status">
                      <th mat-header-cell *matHeaderCellDef>Status</th>
                      <td mat-cell *matCellDef="let inst">
                        <span class="status-badge" [class.enabled]="inst.isEnabled" [class.disabled]="!inst.isEnabled">
                          {{ inst.isEnabled ? 'Active' : 'Disabled' }}
                        </span>
                      </td>
                    </ng-container>

                    <ng-container matColumnDef="actions">
                      <th mat-header-cell *matHeaderCellDef>Actions</th>
                      <td mat-cell *matCellDef="let inst">
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
                    <tr mat-row *matRowDef="let row; columns: instanceColumns"></tr>
                  </table>
                } @else {
                  <div class="empty-state">
                    <mat-icon>folder_off</mat-icon>
                    <p>No projects configured yet.</p>
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
            <mat-card>
              <mat-card-header>
                <mat-card-title>Scan History</mat-card-title>
                <span class="spacer"></span>
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
                  <table mat-table [dataSource]="scans()!.items" class="full-width">
                    <ng-container matColumnDef="id">
                      <th mat-header-cell *matHeaderCellDef>ID</th>
                      <td mat-cell *matCellDef="let scan">#{{ scan.id }}</td>
                    </ng-container>

                    <ng-container matColumnDef="instance">
                      <th mat-header-cell *matHeaderCellDef>Project</th>
                      <td mat-cell *matCellDef="let scan">{{ scan.instanceName ?? 'N/A' }}</td>
                    </ng-container>

                    <ng-container matColumnDef="startedAt">
                      <th mat-header-cell *matHeaderCellDef>Started</th>
                      <td mat-cell *matCellDef="let scan">{{ scan.startedAt | date : 'medium' }}</td>
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
                            <span class="no-vulns">None</span>
                          }
                        </div>
                      </td>
                    </ng-container>

                    <ng-container matColumnDef="triggeredBy">
                      <th mat-header-cell *matHeaderCellDef>Triggered By</th>
                      <td mat-cell *matCellDef="let scan">{{ scan.triggeredBy ?? 'System' }}</td>
                    </ng-container>

                    <tr mat-header-row *matHeaderRowDef="scanColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: scanColumns"></tr>
                  </table>

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
                  <p class="no-data">No scan history records found.</p>
                }
              </mat-card-content>
            </mat-card>
          </div>
        </mat-tab>
      </mat-tab-group>
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

    .tab-content {
      padding: 16px 0;
    }

    .form-card {
      margin-bottom: 16px;
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
      gap: 8px;
      margin-top: 16px;
    }

    .spacer {
      flex: 1;
    }

    .full-width {
      width: 100%;
    }

    .loading-container {
      display: flex;
      justify-content: center;
      padding: 32px;
    }

    .empty-state {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 48px;
      color: rgba(0, 0, 0, 0.38);
    }

    .empty-state mat-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      margin-bottom: 16px;
    }

    .empty-state p {
      margin-bottom: 16px;
    }

    .text-muted {
      color: rgba(0, 0, 0, 0.54);
    }

    .url-text {
      font-family: monospace;
      font-size: 12px;
      color: rgba(0, 0, 0, 0.6);
    }

    .stat {
      display: inline-block;
      padding: 2px 8px;
      margin-right: 4px;
      background: #f5f5f5;
      border-radius: 4px;
      font-size: 12px;
    }

    .status-badge {
      padding: 2px 8px;
      border-radius: 12px;
      font-size: 12px;
      font-weight: 500;
    }

    .status-badge.enabled {
      background: #e8f5e9;
      color: #2e7d32;
    }
    .status-badge.disabled {
      background: #ffebee;
      color: #c62828;
    }
    .status-badge.completed {
      background: #e8f5e9;
      color: #2e7d32;
    }
    .status-badge.running {
      background: #e3f2fd;
      color: #1565c0;
    }
    .status-badge.queued {
      background: #fff8e1;
      color: #f9a825;
    }
    .status-badge.failed {
      background: #ffebee;
      color: #c62828;
    }

    .vuln-summary {
      display: flex;
      gap: 4px;
    }

    .severity-count {
      padding: 1px 6px;
      border-radius: 4px;
      font-size: 11px;
      font-weight: 600;
    }

    .severity-count.critical {
      background: #ffcdd2;
      color: #c62828;
    }
    .severity-count.high {
      background: #ffe0b2;
      color: #e65100;
    }
    .severity-count.medium {
      background: #fff9c4;
      color: #f9a825;
    }
    .severity-count.low {
      background: #c8e6c9;
      color: #2e7d32;
    }

    .no-vulns {
      color: #2e7d32;
      font-size: 12px;
    }

    .no-data {
      text-align: center;
      padding: 32px;
      color: rgba(0, 0, 0, 0.38);
    }
  `,
})
export class ScansComponent implements OnInit {
  readonly loading = signal(true);
  readonly loadingInstances = signal(true);
  readonly triggering = signal(false);
  readonly addingInstance = signal(false);
  readonly showAddInstance = signal(false);
  readonly scans = signal<PagedResult<ScanRun> | null>(null);
  readonly instances = signal<InstanceDto[]>([]);

  newInstance: CreateInstanceRequest = {
    name: '',
    projectUrl: '',
    username: '',
    password: '',
    branch: 'main',
  };

  currentPage = 1;
  pageSize = 25;

  readonly instanceColumns = ['name', 'url', 'stats', 'status', 'actions'];
  readonly scanColumns = ['id', 'instance', 'startedAt', 'duration', 'status', 'vulnerabilities', 'triggeredBy'];

  constructor(
    private readonly apiService: ApiService,
    private readonly snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.loadInstances();
    this.loadScans();
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
    this.apiService.getScanHistory(this.currentPage, this.pageSize).subscribe({
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

  addInstance(): void {
    if (!this.newInstance.name || !this.newInstance.projectUrl || !this.newInstance.username || !this.newInstance.password) {
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
          this.snackBar.open(response.message ?? 'Failed to add project.', 'Close', { duration: 5000 });
        }
      },
      error: (err) => {
        this.addingInstance.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to add project.', 'Close', { duration: 5000 });
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

  triggerScanForInstance(instanceId: number): void {
    this.triggering.set(true);
    this.apiService.triggerScan({ instanceId }).subscribe({
      next: (response) => {
        this.triggering.set(false);
        if (response.success && response.data) {
          this.snackBar.open(response.data.message, 'Close', { duration: 5000 });
          this.loadScans();
          this.loadInstances();
        } else {
          this.snackBar.open(response.message ?? 'Failed to trigger scan.', 'Close', { duration: 5000 });
        }
      },
      error: (err) => {
        this.triggering.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to trigger scan.', 'Close', { duration: 5000 });
      },
    });
  }

  deleteInstance(id: number): void {
    if (!confirm('Are you sure you want to delete this project? This action cannot be undone.')) {
      return;
    }

    this.apiService.deleteInstance(id).subscribe({
      next: (response) => {
        if (response.success) {
          this.snackBar.open('Project deleted.', 'Close', { duration: 5000 });
          this.loadInstances();
        } else {
          this.snackBar.open(response.message ?? 'Failed to delete project.', 'Close', { duration: 5000 });
        }
      },
      error: (err) => {
        this.snackBar.open(err.error?.message ?? 'Failed to delete project.', 'Close', { duration: 5000 });
      },
    });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadScans();
  }
}
