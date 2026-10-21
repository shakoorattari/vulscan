import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, throwError, finalize, shareReplay, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  AuthConfig,
  ChangePasswordRequest,
  LoginRequest,
  LoginResponse,
  UserInfo,
} from '../models/api.models';

const TOKEN_KEY = 'vulscan_access_token';
const REFRESH_TOKEN_KEY = 'vulscan_refresh_token';
const USER_KEY = 'vulscan_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentUser = signal<UserInfo | null>(this.loadStoredUser());
  private readonly accessToken = signal<string | null>(localStorage.getItem(TOKEN_KEY));

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => !!this.currentUser());
  readonly isAdmin = computed(() => this.currentUser()?.role === 'Admin');

  /** True while the signed-in user must change their password (read from the token the server issued). */
  readonly mustChangePassword = computed(() => {
    const claims = this.decode(this.accessToken());
    return claims?.['must_change_password'] === 'true';
  });

  private refreshInFlight$: Observable<LoginResponse> | null = null;

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
  ) {}

  // ── Sign-in methods ───────────────────────────────────────────────────

  getConfig(): Observable<ApiResponse<AuthConfig>> {
    return this.http.get<ApiResponse<AuthConfig>>(`${environment.apiUrl}/auth/config`);
  }

  login(request: LoginRequest): Observable<ApiResponse<LoginResponse>> {
    return this.http
      .post<ApiResponse<LoginResponse>>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap((response) => this.acceptSession(response)));
  }

  /** Exchanges a Microsoft Entra ID token for a Vulscan session. */
  loginWithEntra(idToken: string): Observable<ApiResponse<LoginResponse>> {
    return this.http
      .post<ApiResponse<LoginResponse>>(`${environment.apiUrl}/auth/entra`, { idToken })
      .pipe(tap((response) => this.acceptSession(response)));
  }

  changePassword(request: ChangePasswordRequest): Observable<ApiResponse<LoginResponse>> {
    return this.http
      .post<ApiResponse<LoginResponse>>(`${environment.apiUrl}/auth/change-password`, request)
      .pipe(tap((response) => this.acceptSession(response)));
  }

  // ── Session lifetime ──────────────────────────────────────────────────

  /**
   * Rotates the refresh token. Concurrent callers share one request: refresh tokens are single-use, so two
   * simultaneous refreshes would make the second look like token theft and end the session.
   */
  refresh(): Observable<LoginResponse> {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (!refreshToken) return throwError(() => new Error('No refresh token'));

    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.http
        .post<ApiResponse<LoginResponse>>(`${environment.apiUrl}/auth/refresh`, { refreshToken })
        .pipe(
          map((res) => {
            if (!res.success || !res.data) throw new Error(res.message ?? 'Refresh failed');
            this.storeSession(res.data);
            return res.data;
          }),
          catchError((err) => {
            this.clearSession();
            return throwError(() => err);
          }),
          finalize(() => (this.refreshInFlight$ = null)),
          shareReplay({ bufferSize: 1, refCount: false }),
        );
    }
    return this.refreshInFlight$;
  }

  hasRefreshToken(): boolean {
    return !!localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  /** Ends the session: tells the server to revoke it, then clears local state and returns to the sign-in page. */
  logout(): void {
    const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
    if (refreshToken) {
      // Fire and forget: signing out locally must not depend on the network.
      this.http.post(`${environment.apiUrl}/auth/logout`, { refreshToken }).subscribe({ error: () => undefined });
    }
    this.clearSession();
    this.router.navigate(['/login']);
  }

  getAccessToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  isTokenExpired(skewSeconds = 0): boolean {
    const exp = this.decode(this.getAccessToken())?.['exp'];
    return typeof exp !== 'number' || exp * 1000 < Date.now() + skewSeconds * 1000;
  }

  // ── internals ─────────────────────────────────────────────────────────

  private acceptSession(response: ApiResponse<LoginResponse>): void {
    if (response.success && response.data) this.storeSession(response.data);
  }

  private storeSession(session: LoginResponse): void {
    localStorage.setItem(TOKEN_KEY, session.accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, session.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(session.user));
    this.accessToken.set(session.accessToken);
    this.currentUser.set(session.user);
  }

  private clearSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.accessToken.set(null);
    this.currentUser.set(null);
  }

  private decode(token: string | null): Record<string, unknown> | null {
    if (!token) return null;
    try {
      const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      return JSON.parse(atob(payload));
    } catch {
      return null;
    }
  }

  private loadStoredUser(): UserInfo | null {
    try {
      const stored = localStorage.getItem(USER_KEY);
      return stored ? JSON.parse(stored) : null;
    } catch {
      return null;
    }
  }
}
