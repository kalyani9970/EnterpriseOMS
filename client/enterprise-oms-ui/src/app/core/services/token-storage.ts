import { Injectable } from '@angular/core';

/**
 * Persists ONLY the refresh token, so a page reload can restore the session.
 * The short-lived access token lives in memory (a signal in AuthService) and is never stored.
 *
 * Security trade-off: anything in localStorage can be read by injected JavaScript (XSS).
 * The hardened alternative is an HttpOnly, Secure, SameSite cookie set by the API, which
 * JavaScript cannot read. We keep localStorage for now and revisit it in the hardening phase.
 */
@Injectable({ providedIn: 'root' })
export class TokenStorage {
  private readonly key = 'oms.refresh_token';

  get(): string | null {
    try {
      return localStorage.getItem(this.key);
    } catch {
      return null; // storage blocked (private mode, policy): behave as "no session"
    }
  }

  set(token: string): void {
    try {
      localStorage.setItem(this.key, token);
    } catch {
      // Session simply will not survive a reload.
    }
  }

  clear(): void {
    try {
      localStorage.removeItem(this.key);
    } catch {
      // Nothing to clear.
    }
  }
}