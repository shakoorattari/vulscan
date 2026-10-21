import { Injectable } from '@angular/core';
import type { IPublicClientApplication } from '@azure/msal-browser';
import { AuthConfig } from '../models/api.models';

/**
 * Microsoft Entra ID sign-in via the authorization-code flow with PKCE (MSAL.js). MSAL is used only to obtain an
 * ID token; Vulscan then exchanges it for its own session, so nothing else in the app depends on MSAL state.
 * The library is loaded on demand — it is only needed on the sign-in page.
 */
@Injectable({ providedIn: 'root' })
export class EntraService {
  private msal: IPublicClientApplication | null = null;

  async initialize(config: AuthConfig): Promise<void> {
    if (this.msal || !config.clientId || !config.authority) return;

    const { createStandardPublicClientApplication } = await import('@azure/msal-browser');
    this.msal = await createStandardPublicClientApplication({
      auth: {
        clientId: config.clientId,
        authority: config.authority,
        // Must exactly match a "Single-page application" redirect URI on the Entra app registration.
        redirectUri: `${window.location.origin}/login`,
      },
      cache: { cacheLocation: 'sessionStorage' },
    });
  }

  /** After returning from Microsoft, yields the ID token (once); null on a normal page load. */
  async completeRedirect(): Promise<string | null> {
    // Stay on /login after the redirect so the sign-in page can finish the exchange (MSAL v5 takes this per call).
    const result = await this.msal?.handleRedirectPromise({ navigateToLoginRequestUrl: false });
    return result?.idToken ?? null;
  }

  async signIn(): Promise<void> {
    await this.msal?.loginRedirect({ scopes: ['openid', 'profile', 'email'], prompt: 'select_account' });
  }

  /** Drops MSAL's cached tokens once the Vulscan session exists; they are no longer needed. */
  async clear(): Promise<void> {
    await this.msal?.clearCache();
  }
}
