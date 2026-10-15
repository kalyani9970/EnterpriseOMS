import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { API_BASE_URL } from '../config/api-config';
import { makeAccessToken, makeAuthResult } from '../testing/make-jwt';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  let api: string;
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  function signIn(accessToken = makeAccessToken(), refreshToken = 'refresh-1'): void {
    service.login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(accessToken, refreshToken));
  }

  it('starts signed out', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.user()).toBeNull();
    expect(service.accessToken()).toBeNull();
  });

  it('login posts credentials and builds the user from the token claims', () => {
    service.login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();

    const request = http.expectOne(`${api}/auth/login`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'jane@example.com', password: 'Passw0rd1' });
    request.flush(makeAuthResult(makeAccessToken(), 'refresh-1'));

    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()).toEqual({
      id: 7,
      email: 'jane@example.com',
      fullName: 'Jane Doe',
      role: 'Admin',
      organizationId: 1,
      permissions: ['users.view', 'roles.view'],
    });
    expect(localStorage.getItem('oms.refresh_token')).toBe('refresh-1');
  });

  it('register starts a session just like login', () => {
    service
      .register({ fullName: 'Jane Doe', email: 'jane@example.com', password: 'Passw0rd1', organizationId: 1 })
      .subscribe();

    http.expectOne(`${api}/auth/register`).flush(makeAuthResult(makeAccessToken(), 'refresh-1'));

    expect(service.isAuthenticated()).toBe(true);
  });

  it('treats a single permission claim (serialised as a string) as a one-item list', () => {
    signIn(makeAccessToken({ permission: 'roles.view' }));

    expect(service.user()?.permissions).toEqual(['roles.view']);
  });

  it('handles a token with no permission claim', () => {
    signIn(makeAccessToken({ permission: undefined }));

    expect(service.user()?.permissions).toEqual([]);
  });

  it('hasPermission and hasAnyPermission reflect the token', () => {
    signIn(makeAccessToken({ permission: ['users.view'] }));

    expect(service.hasPermission('users.view')).toBe(true);
    expect(service.hasPermission('roles.view')).toBe(false);
    expect(service.hasAnyPermission('roles.view', 'users.view')).toBe(true);
    expect(service.hasAnyPermission('roles.view', 'orders.create')).toBe(false);
  });

  it('logout clears the session, revokes the token on the server and goes to /login', () => {
    signIn();

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.accessToken()).toBeNull();
    expect(localStorage.getItem('oms.refresh_token')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);

    const request = http.expectOne(`${api}/auth/logout`);
    expect(request.request.body).toEqual({ refreshToken: 'refresh-1' });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('logout still signs out locally when the server call fails', () => {
    signIn();

    service.logout();
    http.expectOne(`${api}/auth/logout`).flush({}, { status: 500, statusText: 'Server Error' });

    expect(service.isAuthenticated()).toBe(false);
  });

  it('logout without a stored token makes no server call', () => {
    service.logout();

    http.expectNone(`${api}/auth/logout`);
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('restoreSession does nothing when there is no refresh token', () => {
    let completed = false;

    service.restoreSession().subscribe({ complete: () => (completed = true) });

    http.expectNone(`${api}/auth/refresh`);
    expect(completed).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
  });

  it('restoreSession turns a stored refresh token into a live session', () => {
    localStorage.setItem('oms.refresh_token', 'stored-token');

    service.restoreSession().subscribe();

    const request = http.expectOne(`${api}/auth/refresh`);
    expect(request.request.body).toEqual({ refreshToken: 'stored-token' });
    request.flush(makeAuthResult(makeAccessToken(), 'rotated-token'));

    expect(service.isAuthenticated()).toBe(true);
    expect(localStorage.getItem('oms.refresh_token')).toBe('rotated-token');
  });

  it('restoreSession swallows a failed refresh and leaves the user signed out', () => {
    localStorage.setItem('oms.refresh_token', 'revoked-token');
    let errored = false;

    service.restoreSession().subscribe({ error: () => (errored = true) });
    http.expectOne(`${api}/auth/refresh`).flush({}, { status: 403, statusText: 'Forbidden' });

    expect(errored).toBe(false);
    expect(service.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('oms.refresh_token')).toBeNull();
  });

  it('refreshSession shares ONE request between concurrent callers', () => {
    localStorage.setItem('oms.refresh_token', 'refresh-1');
    const results: string[] = [];

    service.refreshSession().subscribe((r) => results.push(r.refreshToken));
    service.refreshSession().subscribe((r) => results.push(r.refreshToken));

    // expectOne fails the test if a second refresh request was sent.
    http.expectOne(`${api}/auth/refresh`).flush(makeAuthResult(makeAccessToken(), 'refresh-2'));

    expect(results).toEqual(['refresh-2', 'refresh-2']);
  });

  it('refreshSession allows a new request once the previous one has finished', () => {
    localStorage.setItem('oms.refresh_token', 'refresh-1');

    service.refreshSession().subscribe();
    http.expectOne(`${api}/auth/refresh`).flush(makeAuthResult(makeAccessToken(), 'refresh-2'));

    service.refreshSession().subscribe();
    const second = http.expectOne(`${api}/auth/refresh`);
    expect(second.request.body).toEqual({ refreshToken: 'refresh-2' });
    second.flush(makeAuthResult(makeAccessToken(), 'refresh-3'));
  });

  it('refreshSession errors when there is no refresh token', () => {
    let message = '';

    service.refreshSession().subscribe({ error: (e: Error) => (message = e.message) });

    expect(message).toBe('No refresh token available.');
  });

  it('expireSession clears everything and sends the user to login with a return URL', () => {
    signIn();
    vi.spyOn(TestBed.inject(Router), 'url', 'get').mockReturnValue('/roles');

    service.expireSession();

    expect(service.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/roles', reason: 'expired' },
    });
  });

  it('expireSession omits the return URL when already on the login page', () => {
    vi.spyOn(TestBed.inject(Router), 'url', 'get').mockReturnValue('/login?returnUrl=%2Fhome');

    service.expireSession();

    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { reason: 'expired' } });
  });
});