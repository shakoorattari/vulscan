import { DatePipe } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { AuditLogDto, AuditLogFilter, PagedResult } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSnackBarModule,
  ],
  templateUrl: './audit-log.component.html',
  styleUrl: './admin-page.scss',
})
export class AuditLogComponent implements OnInit {
  readonly loading = signal(true);
  readonly result = signal<PagedResult<AuditLogDto> | null>(null);
  readonly expanded = signal<string | null>(null);

  // Filter model (dates are yyyy-MM-dd from the date inputs)
  from = '';
  to = '';
  username = '';
  action = '';
  outcome = '';
  search = '';

  page = 1;
  pageSize = 50;

  constructor(
    private readonly api: ApiService,
    private readonly snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  apply(): void {
    this.page = 1;
    this.load();
  }

  reset(): void {
    this.from = this.to = this.username = this.action = this.outcome = this.search = '';
    this.apply();
  }

  onPage(e: PageEvent): void {
    this.page = e.pageIndex + 1;
    this.pageSize = e.pageSize;
    this.load();
  }

  toggle(id: string): void {
    this.expanded.update((cur) => (cur === id ? null : id));
  }

  load(): void {
    this.loading.set(true);
    this.api.getAuditLogs({ ...this.filter(), page: this.page, pageSize: this.pageSize }).subscribe({
      next: (res) => {
        this.result.set(res.data ?? null);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.snackBar.open(err.error?.message ?? 'Failed to load the audit log.', 'Close', { duration: 6000 });
      },
    });
  }

  exportCsv(): void {
    this.api.exportAuditLogsCsv(this.filter()).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: (err) => this.snackBar.open(err.error?.message ?? 'Export failed.', 'Close', { duration: 6000 }),
    });
  }

  /** Pretty-prints the details JSON when it is JSON; otherwise shows it verbatim. */
  pretty(details?: string): string {
    if (!details) return '';
    try {
      return JSON.stringify(JSON.parse(details), null, 2);
    } catch {
      return details;
    }
  }

  private filter(): AuditLogFilter {
    return {
      // The server compares UTC timestamps; send whole-day boundaries in UTC.
      from: this.from ? `${this.from}T00:00:00Z` : undefined,
      to: this.to ? `${this.to}T23:59:59Z` : undefined,
      username: this.username.trim() || undefined,
      action: this.action.trim() || undefined,
      outcome: this.outcome || undefined,
      search: this.search.trim() || undefined,
    };
  }
}
