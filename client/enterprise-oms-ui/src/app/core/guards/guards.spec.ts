import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { API_BASE_URL } from '../config/api-config';
import { AuthService } from '../services/auth.service';
import { makeAccessToken, makeAuthResult } from '../testing/make-jwt';
import { authGuard, guestGuard } from './auth.guard';
import { requirePermission } from './permission.guard';

describe('route guards', () => {
  let router: Router;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    router = TestBed.inject(Router);
  });

  function signIn(permissions: string[]): void {
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const api = TestBed.inject(API_BASE_URL);

    auth.login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  function run(guard: CanActivateFn, url = '/roles'): boolean | UrlTree {
    const route = {} as ActivatedRouteSnapshot;
    const state = { url } as RouterStateSnapshot;

    return TestBed.runInInjectionContext(() => guard(route, state)) as boolean | UrlTree;
  }

  describe('authGuard', () => {
    it('redirects anonymous visitors to login and remembers the target', () => {
      const result = run(authGuard, '/roles');

      expect(result).toBeInstanceOf(UrlTree);
      expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Froles');
    });

    it('allows signed-in users', () => {
      signIn(['users.view']);

      expect(run(authGuard)).toBe(true);
    });
  });

  describe('guestGuard', () => {
    it('allows anonymous visitors', () => {
      expect(run(guestGuard)).toBe(true);
    });

    it('sends signed-in users to the home page', () => {
      signIn(['users.view']);

      const result = run(guestGuard);

      expect(router.serializeUrl(result as UrlTree)).toBe('/');
    });
  });

  describe('requirePermission', () => {
    it('allows a user who holds the permission', () => {
      signIn(['roles.view']);

      expect(run(requirePermission('roles.view'))).toBe(true);
    });

    it('sends a signed-in user without the permission to /forbidden', () => {
      signIn(['users.view']);

      const result = run(requirePermission('roles.view'));

      expect(router.serializeUrl(result as UrlTree)).toBe('/forbidden');
    });

    it('requires every listed permission', () => {
      signIn(['roles.view']);

      const result = run(requirePermission('roles.view', 'roles.manage_permissions'));

      expect(router.serializeUrl(result as UrlTree)).toBe('/forbidden');
    });

    it('sends anonymous visitors to login', () => {
      const result = run(requirePermission('roles.view'), '/roles');

      expect(router.serializeUrl(result as UrlTree)).toBe('/login?returnUrl=%2Froles');
    });
  });
});