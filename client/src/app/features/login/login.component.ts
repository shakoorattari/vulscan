import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../core/auth/auth.service';
import { EntraService } from '../../core/auth/entra.service';
import { ApiResponse, AuthConfig, LoginResponse } from '../../core/models/api.models';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnInit {
  username = '';
  password = '';
  readonly hidePassword = signal(true);
  readonly loading = signal(false);
  readonly errorMessage = signal('');

  /** Sign-in methods the server has enabled. Until it answers, neither form is shown. */
  readonly config = signal<AuthConfig | null>(null);
  readonly configError = signal(false);
  /** True while completing a return from Microsoft or redirecting to it. */
  readonly microsoftBusy = signal(false);

  constructor(
    private readonly authService: AuthService,
    private readonly entra: EntraService,
    private readonly router: Router,
  ) {
    // Redirect if already authenticated
    if (this.authService.isAuthenticated() && !this.authService.isTokenExpired()) {
      this.router.navigate([this.authService.mustChangePassword() ? '/change-password' : '/dashboard']);
    }
  }

  ngOnInit(): void {
    this.authService.getConfig().subscribe({
      next: (res) => {
        const cfg = res.data ?? null;
        this.config.set(cfg);
        if (cfg?.entraEnabled) void this.prepareMicrosoft(cfg);
      },
      error: () => this.configError.set(true),
    });
  }

  /** Initialises MSAL and, if we have just come back from Microsoft, completes the sign-in. */
  private async prepareMicrosoft(cfg: AuthConfig): Promise<void> {
    try {
      await this.entra.initialize(cfg);
      const idToken = await this.entra.completeRedirect();
      if (!idToken) return;

      this.microsoftBusy.set(true);
      this.authService.loginWithEntra(idToken).subscribe({
        next: (res) => {
          void this.entra.clear();
          this.onSignedIn(res);
        },
        error: (err) => {
          void this.entra.clear();
          this.microsoftBusy.set(false);
          this.errorMessage.set(err.error?.message ?? 'Microsoft sign-in failed.');
        },
      });
    } catch {
      this.microsoftBusy.set(false);
      this.errorMessage.set('Microsoft sign-in could not be started. Check the Entra configuration.');
    }
  }

  signInWithMicrosoft(): void {
    this.errorMessage.set('');
    this.microsoftBusy.set(true);
    this.entra.signIn().catch(() => {
      this.microsoftBusy.set(false);
      this.errorMessage.set('Could not reach Microsoft sign-in.');
    });
  }

  onLogin(): void {
    if (!this.username || !this.password) {
      this.errorMessage.set('Please enter both username and password.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set('');

    this.authService.login({ username: this.username, password: this.password }).subscribe({
      next: (response) => {
        this.loading.set(false);
        this.onSignedIn(response);
      },
      error: (err) => {
        this.loading.set(false);
        this.password = '';
        this.errorMessage.set(
          err.status === 429
            ? 'Too many attempts. Please wait a minute and try again.'
            : (err.error?.message ?? 'Unable to connect to the server.'),
        );
      },
    });
  }

  private onSignedIn(response: ApiResponse<LoginResponse>): void {
    if (!response.success || !response.data) {
      this.errorMessage.set(response.message ?? 'Login failed.');
      return;
    }
    this.router.navigate([response.data.mustChangePassword ? '/change-password' : '/dashboard']);
  }
}
