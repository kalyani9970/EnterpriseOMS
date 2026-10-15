import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../../../core/config/api-config';
import { makeAccessToken, makeAuthResult } from '../../../core/testing/make-jwt';
import { submitForm, textOf, typeInto } from '../../../core/testing/dom-helpers';
import { Login } from './login';

describe('Login', () => {
  let http: HttpTestingController;
  let api: string;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;
  let harness: RouterTestingHarness;
  let root: HTMLElement;

  async function open(url = '/login'): Promise<void> {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', component: Login }]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);

    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, Login);
    root = harness.routeNativeElement as HTMLElement;

    // Spy only AFTER the harness has navigated to the page under test.
    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  }

  function fillAndSubmit(email = 'jane@example.com', password = 'Passw0rd1'): void {
    typeInto(root, '#email', email);
    typeInto(root, '#password', password);
    submitForm(root);
    harness.detectChanges();
  }

  afterEach(() => http.verify());

  it('shows required errors and sends nothing when the form is empty', async () => {
    await open();

    submitForm(root);
    harness.detectChanges();

    expect(textOf(root)).toContain('Email is required.');
    expect(textOf(root)).toContain('Password is required.');
    http.expectNone(`${api}/auth/login`);
  });

  it('rejects a malformed email without calling the API', async () => {
    await open();

    fillAndSubmit('not-an-email');

    expect(textOf(root)).toContain('Enter a valid email address.');
    http.expectNone(`${api}/auth/login`);
  });

  it('posts the credentials and goes home after a successful sign-in', async () => {
    await open();

    fillAndSubmit();

    const request = http.expectOne(`${api}/auth/login`);
    expect(request.request.body).toEqual({ email: 'jane@example.com', password: 'Passw0rd1' });
    request.flush(makeAuthResult(makeAccessToken(), 'refresh-1'));

    expect(navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('returns the user to the page they were heading to', async () => {
    await open('/login?returnUrl=%2Froles');

    fillAndSubmit();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken(), 'refresh-1'));

    expect(navigateByUrl).toHaveBeenCalledWith('/roles');
  });

  it.each(['//evil.example', 'https://evil.example/phish', '/\\evil.example', 'roles'])(
    'ignores an unsafe returnUrl (%s) to prevent open redirects',
    async (unsafe) => {
      await open(`/login?returnUrl=${encodeURIComponent(unsafe)}`);

      fillAndSubmit();
      http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken(), 'refresh-1'));

      expect(navigateByUrl).toHaveBeenCalledWith('/');
    },
  );

  it('shows the server message for wrong credentials and lets the user retry', async () => {
    await open();

    fillAndSubmit('jane@example.com', 'wrong');
    http
      .expectOne(`${api}/auth/login`)
      .flush(
        { title: 'One or more validation errors occurred.', errors: { Email: ['Invalid email or password.'] } },
        { status: 400, statusText: 'Bad Request' },
      );
    harness.detectChanges();

    expect(textOf(root)).toContain('Invalid email or password.');
    expect(root.querySelector('button[type=submit]')?.hasAttribute('disabled')).toBe(false);
    expect(navigateByUrl).not.toHaveBeenCalled();
  });

  it('disables the button while the request is running', async () => {
    await open();

    fillAndSubmit();

    expect(root.querySelector('button[type=submit]')?.hasAttribute('disabled')).toBe(true);
    expect(textOf(root)).toContain('Signing in…');

    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken(), 'refresh-1'));
  });

  it('explains an unreachable server', async () => {
    await open();

    fillAndSubmit();
    http.expectOne(`${api}/auth/login`).error(new ProgressEvent('error'));
    harness.detectChanges();

    expect(textOf(root)).toContain('Cannot reach the server.');
  });

  it('shows a banner when the session expired', async () => {
    await open('/login?reason=expired');

    expect(textOf(root)).toContain('Your session expired. Please sign in again.');
  });

  it('can reveal and hide the password', async () => {
    await open();
    const input = root.querySelector<HTMLInputElement>('#password')!;
    const toggle = root.querySelector<HTMLButtonElement>('button[aria-label="Show password"]')!;

    expect(input.type).toBe('password');

    toggle.click();
    harness.detectChanges();
    expect(input.type).toBe('text');

    root.querySelector<HTMLButtonElement>('button[aria-label="Hide password"]')!.click();
    harness.detectChanges();
    expect(input.type).toBe('password');
  });
});