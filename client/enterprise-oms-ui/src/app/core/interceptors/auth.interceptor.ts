import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { AuthService } from '../services/auth.service';

/** Endpoints that must never carry a bearer token and never trigger a refresh-and-retry. */
const AUTH_ENDPOINT = /\/auth\/(login|register|refresh|logout)$/;

const withBearer = (request: HttpRequest<unknown>, token: string) =>
  request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });

/**
 * Angular concept: a functional HTTP interceptor sits between HttpClient and the network
 * and can modify every request and response.
 *
 * 1. Attach "Authorization: Bearer <token>" to calls to OUR API only. Never send the token
 *    to another origin.
 * 2. On a 401 (access token expired), refresh once, then replay the failed request with the
 *    new token. If the refresh itself fails, the session is over: send the user to sign in.
 *
 * Only 401 triggers a refresh. A 403 means "signed in but not permitted", which a new token
 * would not fix.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const baseUrl = inject(API_BASE_URL);

  const isApiCall = request.url.startsWith(baseUrl);
  const isAuthEndpoint = AUTH_ENDPOINT.test(request.url);

  if (!isApiCall || isAuthEndpoint) {
    return next(request);
  }

  const token = auth.accessToken();
  const outgoing = token ? withBearer(request, token) : request;

  return next(outgoing).pipe(
    catchError((error: unknown) => {
      const accessTokenRejected =
        error instanceof HttpErrorResponse && error.status === 401 && auth.hasRefreshToken();

      if (!accessTokenRejected) {
        return throwError(() => error);
      }

      return auth.refreshSession().pipe(
        // This catchError sits BEFORE switchMap on purpose: it only sees refresh failures,
        // not errors from the replayed request (for example a legitimate 403).
        catchError((refreshError: unknown) => {
          auth.expireSession();
          return throwError(() => refreshError);
        }),
        switchMap((result) => next(withBearer(request, result.accessToken))),
      );
    }),
  );
};