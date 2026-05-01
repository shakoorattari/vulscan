import { CommonModule } from '@angular/common';
import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import {
  DiscoveredProject,
  DiscoveryListRequest,
  DiscoveryListResponse,
} from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-discovery',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  template: `
    <div class="discovery-page">
      <header class="page-hero">
        <div class="hero-text">
          <h1>Project Discovery</h1>
          <p class="hero-subtitle">
            List all projects on an Azure DevOps server with shared credentials, then bulk-import
            the ones you want to scan.
          </p>
        </div>
      </header>

      <mat-card class="elevated form-card">
        <mat-card-header>
          <mat-card-title>1. Connect to Server</mat-card-title>
          <mat-card-subtitle>Credentials are stored on the instance for shared use.</mat-card-subtitle>
        </mat-card-header>
        <mat-card-content>
          <div class="form-grid">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Server URL</mat-label>
              <input
                matInput
                [(ngModel)]="form.serverUrl"
                placeholder="https://devops.ishj.ae or https://dev.azure.com/myorg"
              />
              <mat-hint>Base URL of the Azure DevOps server (no project)</mat-hint>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Collection</mat-label>
              <input matInput [(ngModel)]="form.collection" placeholder="DefaultCollection or org name" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Username</mat-label>
              <input matInput [(ngModel)]="form.username" placeholder="user@domain.com" />
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Password / PAT</mat-label>
              <input matInput type="password" [(ngModel)]="form.password" />
              <mat-hint>Personal Access Token recommended</mat-hint>
            </mat-form-field>
          </div>

          <div class="form-actions">
            <button
              mat-flat-button
              color="primary"
              (click)="listProjects()"
              [disabled]="listing() || !canList()"
            >
              @if (listing()) {
                <mat-spinner diameter="18"></mat-spinner>
              } @else {
                <ng-container>
                  <mat-icon>search</mat-icon>
                  List Projects
                </ng-container>
              }
            </button>
          </div>
        </mat-card-content>
      </mat-card>

      @if (result(); as r) {
        <mat-card class="elevated">
          <mat-card-header>
            <mat-card-title>2. Select Projects to Import</mat-card-title>
            <mat-card-subtitle>
              Found {{ r.projects.length }} projects on {{ r.serverUrl }}/{{ r.collection }}.
              {{ alreadyImportedCount() }} already imported.
            </mat-card-subtitle>
            <span class="spacer"></span>
            <button mat-stroked-button (click)="toggleAll()">
              {{ allSelected() ? 'Deselect all' : 'Select all importable' }}
            </button>
          </mat-card-header>
          <mat-card-content>
            <div class="import-options">
              <mat-form-field appearance="outline" class="branch-input">
                <mat-label>Default Branch (optional)</mat-label>
                <input matInput [(ngModel)]="defaultBranch" placeholder="main" />
                <mat-hint>Applied to all imported projects</mat-hint>
              </mat-form-field>
            </div>

            <div class="project-list">
              @for (p of r.projects; track p.id) {
                <div class="project-row" [class.disabled]="p.alreadyImported">
                  <mat-checkbox
                    [checked]="isSelected(p.id)"
                    [disabled]="p.alreadyImported"
                    (change)="toggleSelection(p.id)"
                  >
                    <div class="project-meta">
                      <strong>{{ p.name }}</strong>
                      @if (p.description) {
                        <small class="text-muted">{{ p.description }}</small>
                      }
                    </div>
                  </mat-checkbox>
                  @if (p.alreadyImported) {
                    <span class="status-badge enabled">
                      <mat-icon class="check-icon">check_circle</mat-icon>
                      Imported
                    </span>
                  }
                </div>
              }
            </div>

            <div class="form-actions import-actions">
              <span class="selection-summary">
                {{ selectedIds().size }} selected
              </span>
              <button
                mat-flat-button
                color="primary"
                (click)="importSelected()"
                [disabled]="importing() || selectedIds().size === 0"
              >
                @if (importing()) {
                  <mat-spinner diameter="18"></mat-spinner>
                } @else {
                  <ng-container>
                    <mat-icon>cloud_download</mat-icon>
                    Import Selected
                  </ng-container>
                }
              </button>
              <a mat-stroked-button routerLink="/scans">
                <mat-icon>arrow_forward</mat-icon>
                Back to Scans
              </a>
            </div>
          </mat-card-content>
        </mat-card>
      }
    </div>
  `,
  styles: `
    :host { display: block; --gradient-primary: var(--gradient-brand); --shadow-card: var(--shadow-sm); --radius-lg: 16px; }
    .discovery-page { padding: 8px 4px 32px; }
    .page-hero { padding: 24px 28px; margin-bottom: 24px; background: var(--gradient-primary); color: #fff; border-radius: var(--radius-lg); box-shadow: var(--shadow-card); }
    .hero-text h1 { margin: 0 0 4px; font-size: 26px; font-weight: 600; letter-spacing: -0.01em; }
    .hero-subtitle { margin: 0; opacity: 0.9; font-size: 14px; max-width: 720px; }
    .elevated { border-radius: var(--radius-lg); box-shadow: var(--shadow-card); }
    .form-card { margin-bottom: 24px; }
    .form-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 16px; padding-top: 8px; }
    .form-grid .full-width { grid-column: 1 / -1; }
    .form-actions { display: flex; gap: 12px; margin-top: 16px; align-items: center; }
    .import-actions { justify-content: flex-end; padding-top: 16px; border-top: 1px solid rgba(0,0,0,0.06); margin-top: 16px; }
    .selection-summary { margin-right: auto; color: rgba(0,0,0,0.65); font-size: 13px; font-weight: 500; }
    .spacer { flex: 1; }
    .import-options { padding: 8px 0 16px; }
    .branch-input { max-width: 280px; }
    .project-list { display: flex; flex-direction: column; gap: 4px; max-height: 520px; overflow-y: auto; padding: 4px 0; }
    .project-row { display: flex; align-items: center; justify-content: space-between; padding: 10px 12px; border-radius: 8px; transition: background 0.15s ease; }
    .project-row:hover { background: var(--brand-teal-50); }
    .project-row.disabled { opacity: 0.6; }
    .project-meta { display: inline-flex; flex-direction: column; line-height: 1.3; margin-left: 4px; }
    .text-muted { color: rgba(0,0,0,0.55); font-size: 12px; }
    .status-badge { display: inline-flex; align-items: center; gap: 4px; padding: 4px 10px; border-radius: 999px; font-size: 11px; font-weight: 600; line-height: 1; }
    .status-badge.enabled { background: var(--status-success-bg); color: var(--status-success); }
    .check-icon { font-size: 14px; width: 14px; height: 14px; }
    @media (max-width: 768px) {
      .form-grid { grid-template-columns: 1fr; }
    }
  `,
})
export class DiscoveryComponent {
  readonly listing = signal(false);
  readonly importing = signal(false);
  readonly result = signal<DiscoveryListResponse | null>(null);
  readonly selectedIds = signal<Set<string>>(new Set());

  form: DiscoveryListRequest = {
    serverUrl: '',
    collection: '',
    username: '',
    password: '',
  };
  defaultBranch = '';

  readonly alreadyImportedCount = computed(() =>
    this.result()?.projects.filter((p) => p.alreadyImported).length ?? 0,
  );

  readonly allSelected = computed(() => {
    const r = this.result();
    if (!r) return false;
    const importable = r.projects.filter((p) => !p.alreadyImported);
    return importable.length > 0 && importable.every((p) => this.selectedIds().has(p.id));
  });

  constructor(
    private readonly api: ApiService,
    private readonly snackBar: MatSnackBar,
    private readonly router: Router,
  ) {}

  canList(): boolean {
    return !!(this.form.serverUrl && this.form.collection && this.form.username && this.form.password);
  }

  isSelected(id: string): boolean {
    return this.selectedIds().has(id);
  }

  toggleSelection(id: string): void {
    const next = new Set(this.selectedIds());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.selectedIds.set(next);
  }

  toggleAll(): void {
    const r = this.result();
    if (!r) return;
    if (this.allSelected()) {
      this.selectedIds.set(new Set());
    } else {
      const next = new Set<string>(r.projects.filter((p) => !p.alreadyImported).map((p) => p.id));
      this.selectedIds.set(next);
    }
  }

  listProjects(): void {
    this.listing.set(true);
    this.selectedIds.set(new Set());
    this.api.discoverProjects(this.form).subscribe({
      next: (res) => {
        this.listing.set(false);
        if (res.success && res.data) {
          this.result.set(res.data);
        } else {
          this.snackBar.open(res.message ?? 'Failed to list projects.', 'Close', { duration: 5000 });
        }
      },
      error: (err) => {
        this.listing.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to list projects.', 'Close', { duration: 5000 });
      },
    });
  }

  importSelected(): void {
    const r = this.result();
    if (!r || this.selectedIds().size === 0) return;

    const ids = Array.from(this.selectedIds());
    this.importing.set(true);
    this.api
      .importDiscoveredProjects({
        instanceId: r.instanceId,
        azureProjectIds: ids,
        defaultBranch: this.defaultBranch.trim() || undefined,
      })
      .subscribe({
        next: (res) => {
          this.importing.set(false);
          if (res.success && res.data) {
            this.snackBar
              .open(
                `Imported ${res.data.imported}, skipped ${res.data.skipped}.`,
                'View Projects',
                { duration: 6000 },
              )
              .onAction()
              .subscribe(() => this.router.navigate(['/scans']));
            // Re-list to reflect imported state
            this.listProjects();
          } else {
            this.snackBar.open(res.message ?? 'Import failed.', 'Close', { duration: 5000 });
          }
        },
        error: (err) => {
          this.importing.set(false);
          this.snackBar.open(err.error?.message ?? 'Import failed.', 'Close', { duration: 5000 });
        },
      });
  }
}
