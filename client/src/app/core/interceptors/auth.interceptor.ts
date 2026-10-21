import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';

/** Endpoints that must not carry a bearer token or trigger a refresh (they establish or end the session). */
const ANONYMOUS_AUTH_PATHS = ['/auth/login', '/auth/entra', '/auth/refresh', '/auth/config', '/auth/logout'];
const isAnonymousAuthCall = (req: HttpRequest<unknown>) => ANONYMOUS_AUTH_PATHS.some((p) => req.url.includes(p));

/** Authenticated, but a failure here is about the submitted password, not the session — never sign out over it. */
const isPasswordChange = (req: HttpRequest<unknown>) => req.url.includes('/auth/change-password');

const withBearer = (req: HttpRequest<unknown>, token: string) =>
  req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (isAnonymousAuthCall(req)) return next(req);

  const send = (request: HttpRequest<unknown>) => {
    const token = auth.getAccessToken();
    return next(token ? withBearer(request, token) : request);
  };

  // Renew a token that has expired (or is about to) before using it, instead of failing and retrying.
  const proactive =
    auth.getAccessToken() && auth.isTokenExpired(30) && auth.hasRefreshToken()
      ? auth.refresh().pipe(switchMap(() => send(req)))
      : send(req);

  return proactive.pipe(
    catchError((error: HttpErrorResponse) => {
      // The server says the account must change its password first: send the user there.
      if (error.status === 403 && error.error?.errors?.includes?.('PASSWORD_CHANGE_REQUIRED')) {
        if (!router.url.startsWith('/change-password')) router.navigate(['/change-password']);
        return throwError(() => error);
      }

      // Token rejected (revoked, role changed, clock skew): try one silent renewal, then give up.
      if (isPasswordChange(req)) return throwError(() => error);

      if (error.status === 401 && auth.hasRefreshToken()) {
        return auth.refresh().pipe(
          switchMap(() => send(req)),
          catchError((refreshError) => {
            auth.logout();
            return throwError(() => refreshError);
          }),
        );
      }

      if (error.status === 401) auth.logout();
      return throwError(() => error);
    }),
  );
};
