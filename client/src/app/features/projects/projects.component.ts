import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { CreateProjectRequest, ProjectDto } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';
import { ProjectEditDialogComponent } from '../../shared/components/project-edit-dialog.component';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule,
    MatDividerModule,
    MatTooltipModule,
    MatDialogModule,
    MatSlideToggleModule,
  ],
  templateUrl: './projects.component.html',
  styleUrl: './projects.component.scss',
})
export class ProjectsComponent implements OnInit {
  readonly loadingProjects = signal(true);
  readonly triggering = signal(false);
  readonly addingProject = signal(false);
  readonly showAddProject = signal(false);
  readonly projects = signal<ProjectDto[]>([]);

  newProject: CreateProjectRequest = {
    name: '',
    projectUrl: '',
    username: '',
    password: '',
    defaultBranch: '',
    cronExpression: '',
  };

  constructor(
    private readonly apiService: ApiService,
    private readonly snackBar: MatSnackBar,
    private readonly dialog: MatDialog,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.loadProjects();
  }

  // Computed values
  activeProjects = () => this.projects().filter(p => p.isEnabled).length;
  projectsWithVulnerabilities = () => this.projects().filter(p => p.lastScanTotalVulnerabilities > 0).length;
  totalRepositories = () => this.projects().reduce((sum, p) => sum + p.repositoryCount, 0);

  initials(name: string): string {
    if (!name) return '?';
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].substring(0, 2).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  timeAgo(dateString: string): string {
    const date = new Date(dateString);
    const now = new Date();
    const seconds = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (seconds < 60) return 'Just now';
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
    if (seconds < 604800) return `${Math.floor(seconds / 86400)}d ago`;
    return `${Math.floor(seconds / 604800)}w ago`;
  }

  toggleAddProject(): void {
    this.showAddProject.set(!this.showAddProject());
    if (!this.showAddProject()) {
      this.cancelAddProject();
    }
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

  addProject(): void {
    if (!this.newProject.name || !this.newProject.projectUrl) {
      this.snackBar.open('Please fill in required fields.', 'Close', { duration: 3000 });
      return;
    }

    this.addingProject.set(true);
    this.apiService.createProject(this.newProject).subscribe({
      next: (response) => {
        this.addingProject.set(false);
        if (response.success) {
          this.snackBar.open('Project added successfully!', 'Close', { duration: 3000 });
          this.cancelAddProject();
          this.loadProjects();
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
    this.newProject = {
      name: '',
      projectUrl: '',
      username: '',
      password: '',
      defaultBranch: '',
      cronExpression: '',
    };
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
            }
          },
          error: (err) => {
            this.snackBar.open(err.error?.message ?? 'Failed to update project', 'Close', { duration: 5000 });
          },
        });
      }
    });
  }

  triggerScan(projectId: string): void {
    this.triggering.set(true);
    this.apiService.triggerProjectScan(projectId).subscribe({
      next: (response) => {
        this.triggering.set(false);
        if (response.success && response.data) {
          this.snackBar.open(response.data.message, 'Close', { duration: 5000 });
          this.loadProjects();
        }
      },
      error: (err) => {
        this.triggering.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to trigger scan.', 'Close', { duration: 5000 });
      },
    });
  }

  viewScans(project: ProjectDto): void {
    this.router.navigate(['/scans'], { queryParams: { projectId: project.id, projectName: project.name } });
  }
}
