import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ScheduleSettingsDto } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

interface CronPreset {
  label: string;
  expression: string;
  hint: string;
}

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatSnackBarModule,
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
  styles: `
    :host { display: block; }
    .settings-page { max-width: 900px; margin: 0 auto; padding: 24px; }
    .page-header { margin-bottom: 24px; }
    .page-header h1 { display: flex; align-items: center; gap: 12px; margin: 0 0 6px; font-size: 28px; font-weight: 600; }
    .subtitle { margin: 0; color: var(--mat-sys-on-surface-variant); }
    .card { margin-bottom: 16px; border-radius: 16px; }
    .form-grid { display: grid; gap: 16px; grid-template-columns: 1fr auto; align-items: center; }
    .cron-field { width: 100%; }
    .enable-toggle { margin-left: 12px; }
    .presets { display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin: 16px 0 24px; }
    .presets-label { font-size: 13px; color: var(--mat-sys-on-surface-variant); margin-right: 4px; }
    .meta { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; padding: 16px; background: var(--mat-sys-surface-container); border-radius: 12px; }
    .meta-item { display: flex; align-items: center; gap: 12px; }
    .meta-item mat-icon { color: var(--mat-sys-primary); }
    .meta-label { font-size: 12px; color: var(--mat-sys-on-surface-variant); }
    .meta-value { font-size: 14px; font-weight: 500; }
    .actions { display: flex; gap: 12px; margin-top: 24px; justify-content: flex-end; }
    .actions button mat-icon { margin-right: 4px; }
    .hint-card h3 { display: flex; align-items: center; gap: 8px; margin: 0 0 12px; font-size: 16px; }
    .cron-ref { font-family: ui-monospace, Menlo, monospace; font-size: 13px; line-height: 1.4; padding: 12px; background: var(--mat-sys-surface-container); border-radius: 8px; white-space: pre; overflow-x: auto; }
    .hint-note { margin: 12px 0 0; font-size: 13px; color: var(--mat-sys-on-surface-variant); }
    .centered { display: flex; justify-content: center; padding: 48px; }
    @media (max-width: 600px) {
      .form-grid { grid-template-columns: 1fr; }
      .enable-toggle { margin-left: 0; }
      .meta { grid-template-columns: 1fr; }
    }
  `,
})
export class SettingsComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly snack = inject(MatSnackBar);

  readonly settings = signal<ScheduleSettingsDto | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);

  cronInput = '';
  enabledInput = true;

  readonly presets: CronPreset[] = [
    { label: 'Every 15 min', expression: '*/15 * * * *', hint: 'Useful for testing' },
    { label: 'Hourly', expression: '0 * * * *', hint: 'Top of every hour' },
    { label: 'Daily 2am', expression: '0 2 * * *', hint: 'Default off-peak' },
    { label: 'Weekdays 6am', expression: '0 6 * * 1-5', hint: 'Mon–Fri 6:00' },
    { label: 'Weekly Sunday', expression: '0 3 * * 0', hint: 'Sunday at 03:00' },
  ];

  readonly changed = computed(() => {
    const s = this.settings();
    if (!s) return false;
    return this.cronInput.trim() !== s.cronExpression || this.enabledInput !== s.enabled;
  });

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.getScheduleSettings().subscribe({
      next: (res) => {
        if (res.success && res.data) {
          this.settings.set(res.data);
          this.cronInput = res.data.cronExpression;
          this.enabledInput = res.data.enabled;
        }
        this.loading.set(false);
      },
      error: () => {
        this.snack.open('Failed to load schedule settings', 'Dismiss', { duration: 4000 });
        this.loading.set(false);
      },
    });
  }

  applyPreset(expr: string): void {
    this.cronInput = expr;
  }

  reset(): void {
    const s = this.settings();
    if (!s) return;
    this.cronInput = s.cronExpression;
    this.enabledInput = s.enabled;
  }

  save(): void {
    if (!this.cronInput.trim()) {
      this.snack.open('Cron expression is required', 'OK', { duration: 3000 });
      return;
    }
    this.saving.set(true);
    this.api
      .updateScheduleSettings({ cronExpression: this.cronInput.trim(), enabled: this.enabledInput })
      .subscribe({
        next: (res) => {
          this.saving.set(false);
          if (res.success && res.data) {
            this.settings.set(res.data);
            this.cronInput = res.data.cronExpression;
            this.enabledInput = res.data.enabled;
            this.snack.open('Schedule updated', 'OK', { duration: 2500 });
          } else {
            this.snack.open(res.message || 'Update failed', 'Dismiss', { duration: 4000 });
          }
        },
        error: (err) => {
          this.saving.set(false);
          const msg = err?.error?.message || 'Failed to update schedule';
          this.snack.open(msg, 'Dismiss', { duration: 4500 });
        },
      });
  }

  /** Lightweight client-side cron description (for live preview while typing). */
  describe(expr: string): string {
    if (!expr) return ' ';
    const parts = expr.trim().split(/\s+/);
    if (parts.length !== 5) return 'Invalid format — expected 5 fields';
    const [m, h, dom, mon, dow] = parts;
    const num = (v: string) => /^\d+$/.test(v);
    if (mon === '*' && dom === '*' && dow === '*' && num(h) && num(m)) {
      return `Every day at ${h.padStart(2, '0')}:${m.padStart(2, '0')} UTC`;
    }
    if (mon === '*' && dom === '*' && dow === '*' && m === '0' && h.startsWith('*/')) {
      return `Every ${h.slice(2)} hour(s) UTC`;
    }
    if (mon === '*' && dom === '*' && m === '0' && h === '*') return 'Every minute… (heavy!)';
    if (mon === '*' && dom === '*' && dow === '*' && h === '*' && m.startsWith('*/')) {
      return `Every ${m.slice(2)} minute(s) UTC`;
    }
    return `Cron: ${expr} (UTC)`;
  }
}
