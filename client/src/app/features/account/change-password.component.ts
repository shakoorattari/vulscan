import { Component, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/auth/auth.service';

interface Rule {
  label: string;
  met: boolean;
}

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss',
})
export class ChangePasswordComponent {
  currentPassword = '';
  readonly newPassword = signal('');
  confirmPassword = signal('');

  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly hide = signal(true);

  /** Mirrors the server policy (SECURITY.md) for instant feedback; the server is the authority. */
  readonly rules = computed<Rule[]>(() => {
    const p = this.newPassword();
    return [
      { label: 'At least 12 characters', met: p.length >= 12 },
      { label: 'An upper-case letter', met: /[A-Z]/.test(p) },
      { label: 'A lower-case letter', met: /[a-z]/.test(p) },
      { label: 'A digit', met: /\d/.test(p) },
      { label: 'A symbol', met: /[^A-Za-z0-9]/.test(p) },
    ];
  });

  readonly mismatch = computed(() => this.confirmPassword().length > 0 && this.confirmPassword() !== this.newPassword());
  readonly canSubmit = computed(
    () => this.rules().every((r) => r.met) && this.confirmPassword() === this.newPassword() && !this.loading(),
  );

  constructor(
    readonly auth: AuthService,
    private readonly router: Router,
  ) {}

  submit(): void {
    if (!this.canSubmit() || !this.currentPassword) {
      this.errorMessage.set('Enter your current password and a new password that meets every rule.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');
    this.auth.changePassword({ currentPassword: this.currentPassword, newPassword: this.newPassword() }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.message ?? 'Could not change the password.');
      },
    });
  }
}
