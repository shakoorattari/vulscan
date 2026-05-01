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
  templateUrl: './project-config.component.html',
  styleUrl: './project-config.component.scss',
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
  templateUrl: './add-branch-dialog.component.html',
  styleUrl: './add-branch-dialog.component.scss',
})
export class AddBranchDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) public data: { branchNameControl: FormControl }) {}
}
