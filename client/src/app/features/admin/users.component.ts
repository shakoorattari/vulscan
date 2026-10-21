import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ApiResponse, UserDto } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';
import { FormDialogComponent, FormDialogData } from '../../shared/components/form-dialog/form-dialog.component';

const ROLES = [
  { value: 'Admin', label: 'Admin — manage users, settings and audit' },
  { value: 'SecurityAnalyst', label: 'Security analyst — scan, triage, configure projects' },
  { value: 'Viewer', label: 'Viewer — read-only' },
];

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatIconModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  templateUrl: './users.component.html',
  styleUrl: './admin-page.scss',
})
export class UsersComponent implements OnInit {
  readonly loading = signal(true);
  readonly users = signal<UserDto[]>([]);

  readonly total = computed(() => this.users().length);
  readonly active = computed(() => this.users().filter((u) => u.isActive).length);
  readonly entra = computed(() => this.users().filter((u) => u.authProvider === 'Entra').length);
  readonly overdue = computed(() => this.users().filter((u) => u.isActive && u.reviewOverdue).length);

  constructor(
    private readonly api: ApiService,
    private readonly dialog: MatDialog,
    private readonly snackBar: MatSnackBar,
    readonly auth: AuthService,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getUsers().subscribe({
      next: (res) => {
        this.users.set(res.data ?? []);
        this.loading.set(false);
      },
      error: (err) => this.fail(err, 'Failed to load users.', true),
    });
  }

  isSelf(u: UserDto): boolean {
    return u.id === this.auth.user()?.id;
  }

  // ── actions ─────────────────────────────────────────────────────────

  addUser(): void {
    this.ask(
      {
        title: 'Add local user',
        message: 'Creates a username/password account. The person must change this password at first sign-in. Microsoft accounts are created automatically at their first sign-in.',
        confirmLabel: 'Create user',
        fields: [
          { key: 'username', label: 'Username', required: true, minLength: 3, maxLength: 100, autocomplete: 'off' },
          { key: 'email', label: 'Email', type: 'email', required: true },
          { key: 'role', label: 'Role', type: 'select', required: true, value: 'SecurityAnalyst', options: ROLES },
          {
            key: 'password', label: 'Temporary password', type: 'password', required: true, minLength: 12, autocomplete: 'new-password',
            hint: '12+ characters with upper, lower, digit and symbol',
          },
        ],
      },
      (v) => this.api.createUser({ username: v['username'], email: v['email'], password: v['password'], role: v['role'] }),
      'User created.',
    );
  }

  changeRole(u: UserDto): void {
    this.ask(
      { title: `Change role — ${u.username}`, message: 'Their current sessions end so the new role applies immediately.', confirmLabel: 'Change role',
        fields: [{ key: 'role', label: 'Role', type: 'select', required: true, value: u.role, options: ROLES }] },
      (v) => this.api.setUserRole(u.id, v['role']),
      'Role updated.',
    );
  }

  deactivate(u: UserDto): void {
    this.ask(
      { title: `Deactivate ${u.username}`, message: 'Access ends immediately and all sessions are revoked. The account and its history are kept.',
        confirmLabel: 'Deactivate', danger: true,
        fields: [{ key: 'reason', label: 'Reason', type: 'textarea', required: true, minLength: 3, maxLength: 500, hint: 'Recorded in the audit trail' }] },
      (v) => this.api.deactivateUser(u.id, v['reason']),
      'User deactivated.',
    );
  }

  activate(u: UserDto): void {
    this.run(this.api.activateUser(u.id), `${u.username} reactivated.`);
  }

  unlock(u: UserDto): void {
    this.run(this.api.unlockUser(u.id), `${u.username} unlocked.`);
  }

  resetPassword(u: UserDto): void {
    this.ask(
      { title: `Reset password — ${u.username}`, message: 'Sets a temporary password, ends all their sessions, and forces a change at next sign-in.',
        confirmLabel: 'Reset password', danger: true,
        fields: [{ key: 'newPassword', label: 'Temporary password', type: 'password', required: true, minLength: 12, autocomplete: 'new-password',
                   hint: '12+ characters with upper, lower, digit and symbol' }] },
      (v) => this.api.resetUserPassword(u.id, v['newPassword']),
      'Password reset.',
    );
  }

  review(u: UserDto): void {
    this.ask(
      { title: `Confirm access review — ${u.username}`,
        message: `Record that you reviewed this account (${u.role}, ${u.authProvider}) and its access is still appropriate.`,
        confirmLabel: 'Record review',
        fields: [{ key: 'note', label: 'Note (optional)', type: 'textarea', maxLength: 500 }] },
      (v) => this.api.reviewUser(u.id, v['note'] || undefined),
      'Access review recorded.',
    );
  }

  exportCsv(): void {
    this.api.exportAccessReviewCsv().subscribe({
      next: (blob) => this.download(blob, `access-review-${new Date().toISOString().slice(0, 10)}.csv`),
      error: (err) => this.fail(err, 'Export failed.'),
    });
  }

  // ── helpers ─────────────────────────────────────────────────────────

  private ask<T>(data: FormDialogData, call: (values: Record<string, string>) => Observable<ApiResponse<T>>, success: string): void {
    this.dialog.open(FormDialogComponent, { data, width: '460px' }).afterClosed().subscribe((values) => {
      if (values) this.run(call(values), success);
    });
  }

  private run<T>(call: Observable<ApiResponse<T>>, success: string): void {
    call.subscribe({
      next: () => {
        this.snackBar.open(success, 'Close', { duration: 3500 });
        this.load();
      },
      error: (err) => this.fail(err, 'The action failed.'),
    });
  }

  private fail(err: { error?: { message?: string } }, fallback: string, stopLoading = false): void {
    if (stopLoading) this.loading.set(false);
    this.snackBar.open(err.error?.message ?? fallback, 'Close', { duration: 6000 });
  }

  private download(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}
