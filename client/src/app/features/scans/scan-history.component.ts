import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PagedResult, ProjectDto, ScanRun } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-scan-history',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDividerModule,
    MatTooltipModule,
    MatPaginatorModule,
    MatSelectModule,
  ],
  templateUrl: './scan-history.component.html',
  styleUrl: './scan-history.component.scss',
})
export class ScanHistoryComponent implements OnInit {
  readonly loading = signal(true);
  readonly scans = signal<PagedResult<ScanRun> | null>(null);
  readonly filteredProject = signal<string | null>(null);
  readonly filteredProjectName = signal<string | null>(null);

  currentPage = 1;
  pageSize = 12;

  constructor(
    private readonly apiService: ApiService,
    private readonly snackBar: MatSnackBar,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    // Check for query parameters to filter by project
    this.route.queryParams.subscribe(params => {
      if (params['projectId']) {
        this.filteredProject.set(params['projectId']);
        this.filteredProjectName.set(params['projectName'] || 'Selected Project');
      }
      this.loadScans();
    });
  }

  // Computed stats
  totalScans = () => this.scans()?.totalCount ?? 0;
  completedScans = () => this.scans()?.items?.filter(s => s.status === 'Completed').length ?? 0;
  totalVulnerabilities = () => this.scans()?.items?.reduce((sum, s) => sum + s.totalVulnerabilities, 0) ?? 0;
  criticalVulnerabilities = () => this.scans()?.items?.reduce((sum, s) => sum + s.criticalCount, 0) ?? 0;

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

  formatDuration(seconds: number): string {
    if (seconds === 0) return '—';
    if (seconds < 60) return `${seconds}s`;
    const minutes = Math.floor(seconds / 60);
    const remainingSeconds = seconds % 60;
    return `${minutes}m ${remainingSeconds}s`;
  }

  loadScans(): void {
    this.loading.set(true);
    const projectId = this.filteredProject();
    this.apiService.getScanHistory(this.currentPage, this.pageSize, projectId ?? undefined).subscribe({
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

  clearProjectFilter(): void {
    this.filteredProject.set(null);
    this.filteredProjectName.set(null);
    this.currentPage = 1;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {},
      queryParamsHandling: '',
    });
  }

  onPageChange(event: PageEvent): void {
    this.currentPage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadScans();
  }
}
