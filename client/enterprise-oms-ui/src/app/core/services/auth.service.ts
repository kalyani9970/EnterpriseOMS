import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { jwtDecode } from 'jwt-decode';
import { Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { AuthResult, AuthUser, LoginRequest, RegisterRequest } from '../models/auth.models';
import { TokenStorage } from './token-storage';

/** The claims our API puts in the access token (see JwtTokenService on the server). */
interface AccessTokenClaims {
  sub: string;
  email: string;
  full_name: string;
  org_id: string;
  role: string;
  // A JWT with exactly one permission serialises it as a string, with several as an array.
  permission?: string | string[];
  exp: number;
}

/**
 * Owns the client-side session: who is signed in, their token and their permissions.
 *
 * Angular concept: state is held in signals. Components read `user()` or
 * `isAuthenticated()` in templates and re-render automatically when they change.
 * This is what makes the app work without zone.js (zoneless change detection).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly storage = inject(TokenStorage);
  private readonly baseUrl = inject(API_BASE_URL);

  private readonly _accessToken = signal<string | null>(null);
  private readonly _user = signal<AuthUser | null>(null);

  /** The one refresh request currently running, shared by everyone who needs it. */
  private refreshInFlight$: Observable<AuthResult> | null = null;

  readonly accessToken = this._accessToken.asReadonly();
  readonly user = this._user.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);

  private readonly permissionSet = computed(() => new Set(this._user()?.permissions ?? []));

  login(request: LoginRequest): Observable<AuthResult> {
    return this.http
      .post<AuthResult>(`${this.baseUrl}/auth/login`, request)
      .pipe(tap((result) => this.startSession(result)));
  }

  register(request: RegisterRequest): Observable<AuthResult> {
    return this.http
      .post<AuthResult>(`${this.baseUrl}/auth/register`, request)
      .pipe(tap((result) => this.startSession(result)));
  }

  hasRefreshToken(): boolean {
    return this.storage.get() !== null;
  }

  hasPermission(permission: string): boolean {
    return this.permissionSet().has(permission);
  }

  hasAnyPermission(...permissions: string[]): boolean {
    return permissions.some((p) => this.permissionSet().has(p));
  }

  /**
   * Exchanges the stored refresh token for a new token pair.
   *
   * IMPORTANT: the server ROTATES refresh tokens and treats any reuse of an old one as theft,
   * revoking the user's whole token chain. If two requests refreshed at the same moment with
   * the same token, the second would look like theft and log the user out. So all callers share
   * ONE in-flight request: shareReplay hands the same result to every subscriber.
   */
  refreshSession(): Observable<AuthResult> {
    if (this.refreshInFlight$) {
      return this.refreshInFlight$;
    }

    const refreshToken = this.storage.get();
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token available.'));
    }

    this.refreshInFlight$ = this.http
      .post<AuthResult>(`${this.baseUrl}/auth/refresh`, { refreshToken })
      .pipe(
        tap((result) => this.startSession(result)),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );

    return this.refreshInFlight$;
  }

  /**
   * Called once at app start (see provideAppInitializer in app.config.ts).
   * If a refresh token survives from an earlier visit, silently turn it into a live session.
   * Never fails: on any problem the user simply starts signed out.
   */
  restoreSession(): Observable<void> {
    if (!this.hasRefreshToken()) {
      return of(undefined);
    }

    return this.refreshSession().pipe(
      map(() => undefined),
      catchError(() => {
        this.clearSession();
        return of(undefined);
      }),
    );
  }

  /** User-initiated sign out: revoke the refresh token on the server (best effort), then leave. */
  logout(): void {
    const refreshToken = this.storage.get();
    this.clearSession();

    if (refreshToken) {
      this.http.post(`${this.baseUrl}/auth/logout`, { refreshToken }).subscribe({
        error: () => {
          // Best effort: the local session is already gone. The server-side token expires on its own.
        },
      });
    }

    void this.router.navigate(['/login']);
  }

  /** The session can no longer be refreshed (expired, revoked, theft detected): send the user to sign in. */
  expireSession(): void {
    const currentUrl = this.router.url;
    this.clearSession();

    const onAuthPage = currentUrl.startsWith('/login') || currentUrl.startsWith('/register');
    void this.router.navigate(['/login'], {
      queryParams: onAuthPage ? { reason: 'expired' } : { returnUrl: currentUrl, reason: 'expired' },
    });
  }

  private startSession(result: AuthResult): void {
    this.storage.set(result.refreshToken);
    this._accessToken.set(result.accessToken);
    this._user.set(this.toUser(result.accessToken));
  }

  private clearSession(): void {
    this.storage.clear();
    this._accessToken.set(null);
    this._user.set(null);
  }

  private toUser(accessToken: string): AuthUser {
    const claims = jwtDecode<AccessTokenClaims>(accessToken);
    const permission = claims.permission;

    return {
      id: Number(claims.sub),
      email: claims.email,
      fullName: claims.full_name,
      role: claims.role,
      organizationId: Number(claims.org_id),
      permissions: permission === undefined ? [] : Array.isArray(permission) ? permission : [permission],
    };
  }
}