import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
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
  CreateProjectRequest,
  PagedResult,
  ProjectDto,
  ScanRun,
} from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';
import { ProjectEditDialogComponent } from '../../shared/components/project-edit-dialog.component';

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
    MatDialogModule,
  ],
  templateUrl: './scans.component.html',
  styleUrl: './scans.component.scss',
  styles: `
    :host { display: block; --gradient-primary: var(--gradient-brand); --shadow-card: var(--shadow-sm); --shadow-card-hover: var(--shadow-md); --radius-lg: 16px; }
    .scans-page { padding: 8px 4px 32px; }
    .page-hero { display: flex; align-items: center; justify-content: space-between; gap: 24px; padding: 24px 28px; margin-bottom: 24px; background: var(--gradient-primary); color: #fff; border-radius: var(--radius-lg); box-shadow: var(--shadow-card); }
    .hero-text h1 { margin: 0 0 4px; font-size: 26px; font-weight: 600; letter-spacing: -0.01em; }
    .hero-subtitle { margin: 0; opacity: 0.9; font-size: 14px; }
    .hero-actions { display: flex; gap: 10px; }
    .hero-actions button, .hero-actions a { background: rgba(255, 255, 255, 0.95); color: var(--brand-teal-700); }
    .tab-content { padding: 20px 0 0; }
    .elevated { border-radius: var(--radius-lg); box-shadow: var(--shadow-card); transition: box-shadow 0.2s ease; }
    .elevated:hover { box-shadow: var(--shadow-card-hover); }
    .form-card { margin-bottom: 20px; }
    .form-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px; padding-top: 8px; }
    .form-grid .full-width { grid-column: 1 / -1; }
    .form-actions { display: flex; gap: 12px; margin-top: 16px; }
    .spacer { flex: 1; }
    .full-width { width: 100%; }
    .filter-clear { margin-right: 4px; }
    .loading-container { display: flex; justify-content: center; padding: 48px; }
    .empty-state { display: flex; flex-direction: column; align-items: center; text-align: center; padding: 56px 24px; }
    .empty-illustration { width: 80px; height: 80px; border-radius: 50%; background: var(--brand-teal-50); display: flex; align-items: center; justify-content: center; margin-bottom: 20px; }
    .empty-illustration mat-icon { font-size: 40px; width: 40px; height: 40px; color: var(--brand-teal); }
    .empty-state h3 { margin: 0 0 8px; font-weight: 500; }
    .empty-state p { margin: 0 0 20px; color: rgba(0, 0, 0, 0.6); max-width: 360px; }
    .table-wrapper { overflow-x: auto; margin: 4px -8px; padding: 0 8px; }
    .modern-table { width: 100%; background: transparent; }
    .modern-table th.mat-mdc-header-cell { font-weight: 600; color: rgba(0, 0, 0, 0.7); letter-spacing: 0.02em; font-size: 12px; text-transform: uppercase; background: rgba(0, 0, 0, 0.02); }
    .modern-table td.mat-mdc-cell, .modern-table th.mat-mdc-header-cell { padding: 14px 16px; border-bottom: 1px solid rgba(0, 0, 0, 0.06); }
    .data-row { transition: background 0.15s ease; }
    .data-row:hover { background: var(--brand-teal-50); }
    .actions-col { text-align: right; white-space: nowrap; }
    .actions-col button, .actions-col a { margin-left: 6px; }
    .project-cell { display: flex; align-items: center; gap: 12px; }
    .project-avatar { width: 40px; height: 40px; border-radius: 10px; background: var(--gradient-primary); color: #fff; display: flex; align-items: center; justify-content: center; font-weight: 600; font-size: 14px; flex-shrink: 0; }
    .project-info { display: flex; flex-direction: column; line-height: 1.3; }
    .project-info strong { font-size: 14px; }
    .project-info small { font-size: 12px; }
    .text-muted { color: rgba(0, 0, 0, 0.55); }
    .url-text { font-family: 'SF Mono', Menlo, Consolas, monospace; font-size: 11px; color: rgba(0, 0, 0, 0.45); }
    .last-scan-cell { display: flex; flex-direction: column; gap: 6px; }
    .last-scan-meta { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .timestamp { font-size: 12px; }
    .totals-cell { display: flex; gap: 8px; flex-wrap: wrap; }
    .stat-pill { display: inline-flex; align-items: center; gap: 4px; padding: 4px 10px; background: rgba(0, 0, 0, 0.04); border-radius: 999px; font-size: 12px; font-weight: 500; color: rgba(0, 0, 0, 0.75); }
    .stat-pill mat-icon { font-size: 14px; width: 14px; height: 14px; }
    .schedule-chip { font-family: 'SF Mono', Menlo, monospace; font-size: 12px; line-height: 1; padding: 2px 10px !important; min-width: 0 !important; position: relative; }
    .schedule-chip mat-icon { font-size: 14px; width: 14px; height: 14px; margin-right: 4px; vertical-align: middle; }
    .override-dot { display: inline-block; width: 6px; height: 6px; border-radius: 50%; background: var(--brand-teal); margin-left: 6px; vertical-align: middle; }
    .status-badge { display: inline-flex; align-items: center; gap: 6px; padding: 4px 10px; border-radius: 999px; font-size: 11px; font-weight: 600; text-transform: capitalize; line-height: 1; }
    .status-dot { width: 6px; height: 6px; border-radius: 50%; background: currentColor; opacity: 0.85; }
    .status-badge.enabled, .status-badge.completed { background: var(--status-success-bg); color: var(--status-success); }
    .status-badge.disabled, .status-badge.failed { background: var(--status-error-bg); color: var(--status-error); }
    .status-badge.running { background: var(--status-info-bg); color: var(--status-info); }
    .status-badge.queued, .status-badge.pending { background: var(--status-warn-bg); color: var(--status-warn); }
    .vuln-summary { display: flex; gap: 4px; flex-wrap: wrap; align-items: center; }
    .severity-count { padding: 2px 8px; border-radius: 6px; font-size: 11px; font-weight: 700; letter-spacing: 0.02em; }
    .severity-count.critical { background: var(--sev-critical-bg); color: var(--sev-critical); }
    .severity-count.high { background: var(--sev-high-bg); color: var(--sev-high); }
    .severity-count.medium { background: var(--sev-medium-bg); color: var(--sev-medium); }
    .severity-count.low { background: var(--sev-low-bg); color: var(--sev-low); }
    .no-vulns { display: inline-flex; align-items: center; gap: 4px; color: var(--brand-teal-700); font-size: 12px; font-weight: 600; }
    .check-icon { font-size: 16px; width: 16px; height: 16px; }
    .scan-id { font-family: 'SF Mono', Menlo, Consolas, monospace; font-size: 12px; padding: 2px 8px; background: rgba(0, 0, 0, 0.04); border-radius: 6px; color: rgba(0, 0, 0, 0.7); }
    @media (max-width: 768px) {
      .page-hero { flex-direction: column; align-items: flex-start; padding: 20px; }
      .form-grid { grid-template-columns: 1fr; }
      .actions-col { text-align: left; }
      .actions-col button, .actions-col a { margin-left: 0; margin-right: 6px; margin-bottom: 4px; }
    }
  `,
})
export class ScansComponent implements OnInit {
  @ViewChild('tabGroup') tabGroup?: MatTabGroup;

  readonly loading = signal(true);
  readonly loadingProjects = signal(true);
  readonly triggering = signal(false);
  readonly addingProject = signal(false);
  readonly showAddProject = signal(false);
  readonly scans = signal<PagedResult<ScanRun> | null>(null);
  readonly projects = signal<ProjectDto[]>([]);
  readonly filteredProject = signal<ProjectDto | null>(null);

  newProject: CreateProjectRequest = {
    name: '',
    projectUrl: '',
    username: '',
    password: '',
    defaultBranch: '',
    cronExpression: '',
  };

  selectedTab = 0;
  currentPage = 1;
  pageSize = 25;

  readonly projectColumns = ['name', 'lastScan', 'totals', 'schedule', 'status', 'actions'];
  readonly scanColumns = [
    'id',
    'project',
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
    private readonly dialog: MatDialog,
  ) {}

  ngOnInit(): void {
    this.loadProjects();
    this.loadScans();
  }

  initials(name: string): string {
    if (!name) return '?';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  loadProjects(): void {
    this.loadingProjects.set(true);
    this.apiService.getProjects().subscribe({
      next: (response) => {
        this.loadingProjects.set(false);
        if (response.success && response.data) {
          this.projects.set(response.data);
        }
      },
      error: () => {
        this.loadingProjects.set(false);
        this.snackBar.open('Failed to load projects.', 'Close', { duration: 5000 });
      },
    });
  }

  loadScans(): void {
    this.loading.set(true);
    const projectId = this.filteredProject()?.id;
    this.apiService.getScanHistory(this.currentPage, this.pageSize, projectId).subscribe({
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

  viewScansForProject(project: ProjectDto): void {
    this.filteredProject.set(project);
    this.currentPage = 1;
    this.selectedTab = 1;
    this.loadScans();
  }

  clearProjectFilter(): void {
    this.filteredProject.set(null);
    this.currentPage = 1;
    this.loadScans();
  }

  addProject(): void {
    if (
      !this.newProject.name ||
      !this.newProject.projectUrl ||
      !this.newProject.username ||
      !this.newProject.password
    ) {
      this.snackBar.open('Please fill in all required fields.', 'Close', { duration: 5000 });
      return;
    }

    const payload: CreateProjectRequest = {
      ...this.newProject,
      defaultBranch: this.newProject.defaultBranch?.trim() || undefined,
      cronExpression: this.newProject.cronExpression?.trim() || undefined,
    };

    this.addingProject.set(true);
    this.apiService.createProject(payload).subscribe({
      next: (response) => {
        this.addingProject.set(false);
        if (response.success && response.data) {
          this.snackBar.open('Project added successfully!', 'Close', { duration: 5000 });
          this.cancelAddProject();
          this.loadProjects();
        } else {
          this.snackBar.open(response.message ?? 'Failed to add project.', 'Close', { duration: 5000 });
        }
      },
      error: (err) => {
        this.addingProject.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to add project.', 'Close', { duration: 5000 });
      },
    });
  }

  cancelAddProject(): void {
    this.showAddProject.set(false);
    this.newProject = { name: '', projectUrl: '', username: '', password: '', defaultBranch: '', cronExpression: '' };
  }

  editProjectCron(project: ProjectDto): void {
    const current = project.cronExpression ?? '';
    const input = window.prompt(
      `Cron schedule for "${project.name}"\n\nLeave blank to use the global schedule.\nFormat: minute hour day month weekday (e.g. 0 2 * * *)`,
      current,
    );
    if (input === null) return;
    const trimmed = input.trim();
    const cronExpression = trimmed.length === 0 ? undefined : trimmed;
    this.apiService
      .updateProject(project.id, {
        name: project.name,
        isEnabled: project.isEnabled,
        cronExpression,
      })
      .subscribe({
      next: (response) => {
        if (response.success) {
          this.snackBar.open('Schedule updated.', 'Close', { duration: 4000 });
          this.loadProjects();
        } else {
          this.snackBar.open(response.message ?? 'Failed to update schedule.', 'Close', { duration: 5000 });
        }
      },
      error: (err) => {
        this.snackBar.open(err.error?.message ?? 'Invalid cron expression.', 'Close', { duration: 5000 });
      },
    });
  }

  editProject(project: ProjectDto): void {
    const dialogRef = this.dialog.open(ProjectEditDialogComponent, {
      width: '600px',
      data: { project },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.apiService.updateProject(project.id, result).subscribe({
          next: (response) => {
            if (response.success) {
              this.snackBar.open('Project updated successfully', 'Close', { duration: 3000 });
              this.loadProjects();
            } else {
              this.snackBar.open(response.message ?? 'Failed to update project', 'Close', { duration: 5000 });
            }
          },
          error: (err) => {
            this.snackBar.open(err.error?.message ?? 'Failed to update project', 'Close', { duration: 5000 });
          },
        });
      }
    });
  }

  triggerScanForProject(projectId: string): void {
    this.triggering.set(true);
    this.apiService.triggerProjectScan(projectId).subscribe({
      next: (response) => {
        this.triggering.set(false);
        if (response.success && response.data) {
          this.snackBar.open(response.data.message, 'Close', { duration: 5000 });
          this.loadScans();
          this.loadProjects();
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

  deleteProject(id: string): void {
    if (!confirm('Are you sure you want to delete this project? This action cannot be undone.')) return;

    this.apiService.deleteProject(id).subscribe({
      next: (response) => {
        if (response.success) {
          this.snackBar.open('Project deleted.', 'Close', { duration: 5000 });
          if (this.filteredProject()?.id === id) this.clearProjectFilter();
          this.loadProjects();
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
