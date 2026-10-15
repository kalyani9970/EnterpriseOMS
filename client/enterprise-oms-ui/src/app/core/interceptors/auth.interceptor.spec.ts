import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { API_BASE_URL } from '../config/api-config';
import { AuthService } from '../services/auth.service';
import { makeAccessToken, makeAuthResult } from '../testing/make-jwt';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let client: HttpClient;
  let http: HttpTestingController;
  let auth: AuthService;
  let api: string;
  let navigate: ReturnType<typeof vi.spyOn>;

  const oldToken = makeAccessToken({ permission: ['old.permission'] });
  const newToken = makeAccessToken({ permission: ['new.permission'] });

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    client = TestBed.inject(HttpClient);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
    api = TestBed.inject(API_BASE_URL);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  function signIn(accessToken = oldToken, refreshToken = 'refresh-1'): void {
    auth.login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(accessToken, refreshToken));
  }

  const unauthorized = { status: 401, statusText: 'Unauthorized' };

  it('adds the bearer token to API requests', () => {
    signIn();

    client.get(`${api}/roles`).subscribe();

    const request = http.expectOne(`${api}/roles`);
    expect(request.request.headers.get('Authorization')).toBe(`Bearer ${oldToken}`);
    request.flush([]);
  });

  it('sends no Authorization header when signed out', () => {
    client.get(`${api}/roles`).subscribe();

    const request = http.expectOne(`${api}/roles`);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush([]);
  });

  it('never sends the token to another origin', () => {
    signIn();

    client.get('https://example.org/data').subscribe();

    const request = http.expectOne('https://example.org/data');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('does not attach the token to auth endpoints', () => {
    signIn();

    client.post(`${api}/auth/login`, {}).subscribe();

    const request = http.expectOne(`${api}/auth/login`);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
  });

  it('on 401 refreshes once and replays the request with the new token', () => {
    signIn(oldToken, 'refresh-1');
    let result: unknown;

    client.get(`${api}/roles`).subscribe((r) => (result = r));
    http.expectOne(`${api}/roles`).flush({}, unauthorized);

    const refresh = http.expectOne(`${api}/auth/refresh`);
    expect(refresh.request.body).toEqual({ refreshToken: 'refresh-1' });
    refresh.flush(makeAuthResult(newToken, 'refresh-2'));

    const replay = http.expectOne(`${api}/roles`);
    expect(replay.request.headers.get('Authorization')).toBe(`Bearer ${newToken}`);
    replay.flush([{ id: 1 }]);

    expect(result).toEqual([{ id: 1 }]);
  });

  it('several simultaneous 401s trigger exactly ONE refresh and all are replayed', () => {
    signIn(oldToken, 'refresh-1');
    const results: unknown[] = [];

    client.get(`${api}/roles`).subscribe((r) => results.push(r));
    client.get(`${api}/products`).subscribe((r) => results.push(r));

    http.expectOne(`${api}/roles`).flush({}, unauthorized);
    http.expectOne(`${api}/products`).flush({}, unauthorized);

    // expectOne fails if a second refresh was started. Reusing a rotated token would look
    // like theft to the server and sign the user out.
    http.expectOne(`${api}/auth/refresh`).flush(makeAuthResult(newToken, 'refresh-2'));

    http.expectOne(`${api}/roles`).flush(['roles']);
    http.expectOne(`${api}/products`).flush(['products']);

    expect(results).toEqual([['roles'], ['products']]);
  });

  it('when the refresh fails, ends the session and surfaces the error', () => {
    signIn();
    vi.spyOn(TestBed.inject(Router), 'url', 'get').mockReturnValue('/roles');
    let status = 0;

    client.get(`${api}/roles`).subscribe({ error: (e: { status: number }) => (status = e.status) });
    http.expectOne(`${api}/roles`).flush({}, unauthorized);
    http.expectOne(`${api}/auth/refresh`).flush({}, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
    expect(auth.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(['/login'], {
      queryParams: { returnUrl: '/roles', reason: 'expired' },
    });
  });

  it('a 403 from the API is passed through without refreshing', () => {
    signIn();
    let status = 0;

    client.get(`${api}/roles`).subscribe({ error: (e: { status: number }) => (status = e.status) });
    http.expectOne(`${api}/roles`).flush({}, { status: 403, statusText: 'Forbidden' });

    http.expectNone(`${api}/auth/refresh`);
    expect(status).toBe(403);
    expect(auth.isAuthenticated()).toBe(true);
  });

  it('a 403 on the replayed request does not end the session', () => {
    signIn(oldToken, 'refresh-1');
    let status = 0;

    client.get(`${api}/roles`).subscribe({ error: (e: { status: number }) => (status = e.status) });
    http.expectOne(`${api}/roles`).flush({}, unauthorized);
    http.expectOne(`${api}/auth/refresh`).flush(makeAuthResult(newToken, 'refresh-2'));
    http.expectOne(`${api}/roles`).flush({}, { status: 403, statusText: 'Forbidden' });

    expect(status).toBe(403);
    expect(auth.isAuthenticated()).toBe(true);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('a 401 with no refresh token available is passed through', () => {
    let status = 0;

    client.get(`${api}/roles`).subscribe({ error: (e: { status: number }) => (status = e.status) });
    http.expectOne(`${api}/roles`).flush({}, unauthorized);

    http.expectNone(`${api}/auth/refresh`);
    expect(status).toBe(401);
  });
});