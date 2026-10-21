import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

/** True while there is a usable session: a live access token, or a refresh token that can renew it. */
const hasSession = (auth: AuthService): boolean =>
  auth.isAuthenticated() && (!auth.isTokenExpired() || auth.hasRefreshToken());

/** Signed in (session only). Used by the change-password page, which is where a forced change must be completed. */
export const sessionGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (hasSession(auth)) return true;

  auth.logout();
  return router.createUrlTree(['/login']);
};

/** Signed in, and not blocked by a pending password change. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!hasSession(auth)) {
    auth.logout();
    return router.createUrlTree(['/login']);
  }

  // The server refuses everything else until the password is changed; send the user straight there.
  if (auth.mustChangePassword()) return router.createUrlTree(['/change-password']);

  return true;
};

export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAdmin()) {
    return true;
  }

  return router.createUrlTree(['/dashboard']);
};
