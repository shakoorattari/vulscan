import { CommonModule } from '@angular/common';
import { Component, Inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router } from '@angular/router';
import {
  BranchConfigDto,
  ProjectConfigurationDto,
  RepositoryConfigDto,
} from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-project-config',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatTableModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatExpansionModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatInputModule,
    MatDialogModule,
    MatSlideToggleModule,
  ],
  template: `
    <div class="project-config">
      <div class="page-header">
        <div class="breadcrumb">
          <a class="back-link" (click)="goBack()">
            <mat-icon>arrow_back</mat-icon> Projects
          </a>
          <mat-icon class="separator">chevron_right</mat-icon>
          <span class="current-page">{{ config()?.name ?? 'Project Configuration' }}</span>
        </div>
        <div class="header-actions">
          <button
            mat-flat-button
            color="primary"
            (click)="refreshConfig()"
            [disabled]="loading()"
            matTooltip="Refresh configuration"
          >
            <mat-icon>refresh</mat-icon> Refresh
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="loading-container">
          <mat-spinner diameter="48"></mat-spinner>
        </div>
      } @else if (config()) {
        <!-- Project Summary -->
        <mat-card class="summary-card">
          <mat-card-header>
            <mat-card-title>
              <mat-icon>settings</mat-icon> Configuration Summary
            </mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div class="summary-grid">
              <div class="summary-item">
                <span class="label">Total Repositories:</span>
                <span class="value">{{ config()!.totalRepositories }}</span>
              </div>
              <div class="summary-item">
                <span class="label">Enabled Repositories:</span>
                <span class="value">{{ config()!.enabledRepositories }}</span>
              </div>
              <div class="summary-item">
                <span class="label">Total Configured Branches:</span>
                <span class="value">{{ config()!.totalConfiguredBranches }}</span>
              </div>
              <div class="summary-item">
                <span class="label">Project Default Branch:</span>
                <span class="value">{{ config()!.defaultBranch || 'None' }}</span>
              </div>
            </div>
          </mat-card-content>
        </mat-card>

        <!-- Repositories -->
        <h3 class="section-title">
          <mat-icon>source</mat-icon> Repositories ({{ config()!.totalRepositories }})
        </h3>

        <mat-accordion multi>
          @for (repo of config()!.repositories; track repo.id) {
            <mat-expansion-panel>
              <mat-expansion-panel-header>
                <mat-panel-title>
                  <mat-icon [class.disabled]="!repo.isEnabled">folder</mat-icon>
                  {{ repo.name }}
                  @if (!repo.isEnabled) {
                    <mat-chip class="disabled-chip">Disabled</mat-chip>
                  }
                </mat-panel-title>
                <mat-panel-description>
                  Default: {{ repo.defaultBranch }} ·
                  {{ repo.enabledBranches }} / {{ repo.totalBranches }} branches enabled
                  @if (repo.lastScannedAt) {
                    · Last scanned: {{ repo.lastScannedAt | date: 'short' }}
                  }
                </mat-panel-description>
              </mat-expansion-panel-header>

              <!-- Repository Details -->
              <div class="repo-details">
                <div class="repo-info">
                  <p><strong>Clone URL:</strong> {{ repo.cloneUrl }}</p>
                  <p><strong>Default Branch:</strong> {{ repo.defaultBranch }}</p>
                  @if (repo.lastScannedCommit) {
                    <p><strong>Last Scanned Commit:</strong> {{ repo.lastScannedCommit }}</p>
                  }
                </div>

                <div class="repo-actions">
                  <button
                    mat-stroked-button
                    [color]="repo.isEnabled ? 'warn' : 'primary'"
                    (click)="toggleRepository(repo)"
                  >
                    <mat-icon>{{ repo.isEnabled ? 'block' : 'check_circle' }}</mat-icon>
                    {{ repo.isEnabled ? 'Disable' : 'Enable' }} Repository
                  </button>
                </div>
              </div>

              <mat-divider></mat-divider>

              <!-- Configured Branches -->
              <div class="branches-section">
                <div class="branches-header">
                  <h4>
                    <mat-icon>account_tree</mat-icon> Configured Branches ({{ repo.totalBranches }})
                  </h4>
                  <button
                    mat-flat-button
                    color="primary"
                    (click)="openAddBranchDialog(repo)"
                    [disabled]="!repo.isEnabled"
                  >
                    <mat-icon>add</mat-icon> Add Branch
                  </button>
                </div>

                @if (repo.configuredBranches.length === 0) {
                  <div class="no-branches">
                    <mat-icon>info</mat-icon>
                    <p>
                      No branches configured. The default branch will be used for scanning.
                      <br />
                      Add branches to scan multiple branches in this repository.
                    </p>
                  </div>
                } @else {
                  <table mat-table [dataSource]="repo.configuredBranches" class="branches-table">
                    <ng-container matColumnDef="branchName">
                      <th mat-header-cell *matHeaderCellDef>Branch Name</th>
                      <td mat-cell *matCellDef="let branch">
                        <mat-icon class="branch-icon">account_tree</mat-icon>
                        {{ branch.branchName }}
                      </td>
                    </ng-container>

                    <ng-container matColumnDef="isEnabled">
                      <th mat-header-cell *matHeaderCellDef>Status</th>
                      <td mat-cell *matCellDef="let branch">
                        <mat-chip [class.enabled]="branch.isEnabled" [class.disabled]="!branch.isEnabled">
                          {{ branch.isEnabled ? 'Enabled' : 'Disabled' }}
                        </mat-chip>
                      </td>
                    </ng-container>

                    <ng-container matColumnDef="scanCount">
                      <th mat-header-cell *matHeaderCellDef>Scans</th>
                      <td mat-cell *matCellDef="let branch">{{ branch.scanCount }}</td>
                    </ng-container>

                    <ng-container matColumnDef="lastScannedAt">
                      <th mat-header-cell *matHeaderCellDef>Last Scanned</th>
                      <td mat-cell *matCellDef="let branch">
                        {{ branch.lastScannedAt ? (branch.lastScannedAt | date: 'short') : '—' }}
                      </td>
                    </ng-container>

                    <ng-container matColumnDef="actions">
                      <th mat-header-cell *matHeaderCellDef>Actions</th>
                      <td mat-cell *matCellDef="let branch">
                        <button
                          mat-icon-button
                          [color]="branch.isEnabled ? 'warn' : 'primary'"
                          (click)="toggleBranch(repo, branch)"
                          [matTooltip]="branch.isEnabled ? 'Disable branch' : 'Enable branch'"
                        >
                          <mat-icon>{{ branch.isEnabled ? 'toggle_on' : 'toggle_off' }}</mat-icon>
                        </button>
                        <button
                          mat-icon-button
                          color="warn"
                          (click)="deleteBranch(repo, branch)"
                          matTooltip="Remove branch"
                        >
                          <mat-icon>delete</mat-icon>
                        </button>
                      </td>
                    </ng-container>

                    <tr mat-header-row *matHeaderRowDef="branchColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: branchColumns"></tr>
                  </table>
                }
              </div>
            </mat-expansion-panel>
          }
        </mat-accordion>

        @if (config()!.repositories.length === 0) {
          <mat-card class="empty-state">
            <mat-card-content>
              <mat-icon>info</mat-icon>
              <p>No repositories found for this project. Run discovery to add repositories.</p>
            </mat-card-content>
          </mat-card>
        }
      } @else {
        <mat-card class="error-card">
          <mat-card-content>
            <mat-icon>error</mat-icon>
            <p>Failed to load project configuration.</p>
          </mat-card-content>
        </mat-card>
      }
    </div>
  `,
  styles: [
    `
      .project-config {
        padding: 24px;
        max-width: 1400px;
        margin: 0 auto;
      }

      .page-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 24px;
      }

      .breadcrumb {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 14px;
      }

      .back-link {
        cursor: pointer;
        display: flex;
        align-items: center;
        gap: 4px;
        color: #1976d2;
        text-decoration: none;
      }

      .back-link:hover {
        text-decoration: underline;
      }

      .separator {
        font-size: 16px;
        color: #999;
      }

      .current-page {
        font-weight: 500;
        font-size: 18px;
      }

      .header-actions {
        display: flex;
        gap: 12px;
      }

      .loading-container {
        display: flex;
        justify-content: center;
        padding: 48px;
      }

      .summary-card {
        margin-bottom: 24px;
      }

      mat-card-title {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      .summary-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
        gap: 16px;
      }

      .summary-item {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }

      .summary-item .label {
        font-size: 12px;
        color: #666;
        text-transform: uppercase;
        letter-spacing: 0.5px;
      }

      .summary-item .value {
        font-size: 20px;
        font-weight: 500;
      }

      .section-title {
        display: flex;
        align-items: center;
        gap: 8px;
        margin: 24px 0 16px;
        font-size: 18px;
        font-weight: 500;
      }

      mat-expansion-panel {
        margin-bottom: 12px;
      }

      mat-panel-title {
        display: flex;
        align-items: center;
        gap: 8px;
      }

      mat-panel-title mat-icon.disabled {
        opacity: 0.5;
      }

      .disabled-chip {
        margin-left: 8px;
        background-color: #f44336 !important;
        color: white !important;
        font-size: 11px;
        height: 20px;
        padding: 0 8px;
      }

      .repo-details {
        padding: 16px 0;
      }

      .repo-info {
        margin-bottom: 16px;
      }

      .repo-info p {
        margin: 8px 0;
        font-size: 14px;
      }

      .repo-actions {
        display: flex;
        gap: 12px;
        margin-bottom: 16px;
      }

      .branches-section {
        padding: 16px 0;
      }

      .branches-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 16px;
      }

      .branches-header h4 {
        display: flex;
        align-items: center;
        gap: 8px;
        margin: 0;
        font-size: 16px;
      }

      .no-branches {
        text-align: center;
        padding: 32px;
        color: #666;
      }

      .no-branches mat-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
        margin-bottom: 16px;
        opacity: 0.5;
      }

      .branches-table {
        width: 100%;
        margin-top: 16px;
      }

      .branch-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
        margin-right: 8px;
        vertical-align: middle;
      }

      mat-chip.enabled {
        background-color: #4caf50 !important;
        color: white !important;
      }

      mat-chip.disabled {
        background-color: #999 !important;
        color: white !important;
      }

      .empty-state,
      .error-card {
        text-align: center;
        padding: 48px;
      }

      .empty-state mat-icon,
      .error-card mat-icon {
        font-size: 64px;
        width: 64px;
        height: 64px;
        margin-bottom: 16px;
        opacity: 0.5;
      }

      .error-card mat-icon {
        color: #f44336;
      }
    `,
  ],
})
export class ProjectConfigComponent implements OnInit {
  projectId = '';
  config = signal<ProjectConfigurationDto | null>(null);
  loading = signal(true);
  branchColumns = ['branchName', 'isEnabled', 'scanCount', 'lastScannedAt', 'actions'];

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly api: ApiService,
    private readonly dialog: MatDialog,
    private readonly snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.projectId = this.route.snapshot.paramMap.get('projectId') ?? '';
    if (this.projectId) {
      this.loadConfiguration();
    } else {
      this.loading.set(false);
      this.snackBar.open('Invalid project ID', 'Close', { duration: 3000 });
    }
  }

  loadConfiguration(): void {
    this.loading.set(true);
    this.api.getProjectConfiguration(this.projectId).subscribe({
      next: (response) => {
        this.config.set(response.data ?? null);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load configuration:', err);
        this.snackBar.open('Failed to load project configuration', 'Close', { duration: 3000 });
        this.loading.set(false);
      },
    });
  }

  refreshConfig(): void {
    this.loadConfiguration();
  }

  goBack(): void {
    this.router.navigate(['/reports']);
  }

  toggleRepository(repo: RepositoryConfigDto): void {
    const newStatus = !repo.isEnabled;
    this.api.updateRepository(repo.id, { isEnabled: newStatus }).subscribe({
      next: () => {
        this.snackBar.open(
          `Repository ${newStatus ? 'enabled' : 'disabled'}`,
          'Close',
          { duration: 2000 },
        );
        this.loadConfiguration();
      },
      error: (err) => {
        console.error('Failed to update repository:', err);
        this.snackBar.open('Failed to update repository', 'Close', { duration: 3000 });
      },
    });
  }

  openAddBranchDialog(repo: RepositoryConfigDto): void {
    const branchNameControl = new FormControl('');
    const dialogRef = this.dialog.open(AddBranchDialogComponent, {
      width: '400px',
      data: { branchNameControl },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.addBranch(repo, result);
      }
    });
  }

  addBranch(repo: RepositoryConfigDto, branchName: string): void {
    this.api.addRepositoryBranch(repo.id, { branchName, isEnabled: true }).subscribe({
      next: () => {
        this.snackBar.open('Branch added successfully', 'Close', { duration: 2000 });
        this.loadConfiguration();
      },
      error: (err) => {
        console.error('Failed to add branch:', err);
        this.snackBar.open(
          err.error?.message || 'Failed to add branch',
          'Close',
          { duration: 3000 },
        );
      },
    });
  }

  toggleBranch(repo: RepositoryConfigDto, branch: BranchConfigDto): void {
    const newStatus = !branch.isEnabled;
    this.api.updateRepositoryBranch(repo.id, branch.id, { isEnabled: newStatus }).subscribe({
      next: () => {
        this.snackBar.open(
          `Branch ${newStatus ? 'enabled' : 'disabled'}`,
          'Close',
          { duration: 2000 },
        );
        this.loadConfiguration();
      },
      error: (err) => {
        console.error('Failed to update branch:', err);
        this.snackBar.open('Failed to update branch', 'Close', { duration: 3000 });
      },
    });
  }

  deleteBranch(repo: RepositoryConfigDto, branch: BranchConfigDto): void {
    if (confirm(`Are you sure you want to remove the branch "${branch.branchName}"?`)) {
      this.api.deleteRepositoryBranch(repo.id, branch.id).subscribe({
        next: () => {
          this.snackBar.open('Branch removed', 'Close', { duration: 2000 });
          this.loadConfiguration();
        },
        error: (err) => {
          console.error('Failed to delete branch:', err);
          this.snackBar.open('Failed to remove branch', 'Close', { duration: 3000 });
        },
      });
    }
  }
}

// Add Branch Dialog Component
@Component({
  selector: 'app-add-branch-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Add Branch</h2>
    <mat-dialog-content>
      <mat-form-field class="full-width">
        <mat-label>Branch Name</mat-label>
        <input
          matInput
          [formControl]="data.branchNameControl"
          placeholder="e.g., main, develop, release/v1.0"
          required
        />
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancel</button>
      <button
        mat-flat-button
        color="primary"
        [mat-dialog-close]="data.branchNameControl.value"
        [disabled]="!data.branchNameControl.value?.trim()"
      >
        Add
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .full-width {
        width: 100%;
      }
    `,
  ],
})
export class AddBranchDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: { branchNameControl: FormControl }) {}
}
