import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { API_BASE_URL } from '../../../core/config/api-config';
import { submitForm, textOf, typeInto } from '../../../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../../../core/testing/make-jwt';
import { Register } from './register';

describe('Register', () => {
  let http: HttpTestingController;
  let api: string;
  let navigateByUrl: ReturnType<typeof vi.spyOn>;
  let harness: RouterTestingHarness;
  let root: HTMLElement;

  beforeEach(async () => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'register', component: Register }]),
      ],
    });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);

    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/register', Register);
    root = harness.routeNativeElement as HTMLElement;

    navigateByUrl = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => http.verify());

  function fill(overrides: Partial<Record<'fullName' | 'email' | 'password' | 'confirmPassword', string>> = {}): void {
    typeInto(root, '#fullName', overrides.fullName ?? 'Jane Doe');
    typeInto(root, '#email', overrides.email ?? 'jane@example.com');
    typeInto(root, '#password', overrides.password ?? 'Passw0rd1');
    typeInto(root, '#confirmPassword', overrides.confirmPassword ?? 'Passw0rd1');
  }

  function submit(): void {
    submitForm(root);
    harness.detectChanges();
  }

  const checklistItems = () =>
    Array.from(root.querySelectorAll('ul[aria-label="Password requirements"] li')).map((li) =>
      (li.textContent ?? '').replace(/\s+/g, ' ').trim(),
    );

  it('starts with the organization defaulted to 1', () => {
    expect(root.querySelector<HTMLInputElement>('#organizationId')?.value).toBe('1');
  });

  it('ticks off the password checklist live as the user types', () => {
    expect(checklistItems().every((item) => item.startsWith('○'))).toBe(true);

    typeInto(root, '#password', 'abc');
    harness.detectChanges();
    expect(checklistItems()).toEqual([
      '○ At least 8 characters',
      '○ One uppercase letter',
      '✓ One lowercase letter',
      '○ One digit',
    ]);

    typeInto(root, '#password', 'Passw0rd1');
    harness.detectChanges();
    expect(checklistItems().every((item) => item.startsWith('✓'))).toBe(true);
  });

  it('shows required errors and sends nothing when the form is empty', () => {
    typeInto(root, '#organizationId', '1');

    submit();

    expect(textOf(root)).toContain('Full name is required.');
    expect(textOf(root)).toContain('Email is required.');
    expect(textOf(root)).toContain('Password is required.');
    http.expectNone(`${api}/auth/register`);
  });

  it('blocks a weak password before it reaches the server', () => {
    fill({ password: 'weak', confirmPassword: 'weak' });

    submit();

    http.expectNone(`${api}/auth/register`);
  });

  it('blocks mismatched passwords and says so', () => {
    fill({ confirmPassword: 'Different1' });

    submit();

    expect(textOf(root)).toContain('Passwords do not match.');
    http.expectNone(`${api}/auth/register`);
  });

  it('rejects an organization ID below 1', () => {
    fill();
    typeInto(root, '#organizationId', '0');

    submit();

    expect(textOf(root)).toContain('Enter a valid organization ID (1 or higher).');
    http.expectNone(`${api}/auth/register`);
  });

  it('posts the account details (without the confirmation field) and goes home', () => {
    fill();

    submit();

    const request = http.expectOne(`${api}/auth/register`);
    expect(request.request.body).toEqual({
      fullName: 'Jane Doe',
      email: 'jane@example.com',
      password: 'Passw0rd1',
      organizationId: 1,
    });
    request.flush(makeAuthResult(makeAccessToken(), 'refresh-1'));

    expect(navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('shows a server validation error under the field it belongs to', () => {
    fill();

    submit();
    http.expectOne(`${api}/auth/register`).flush(
      { title: 'One or more validation errors occurred.', errors: { Email: ['This email is already registered.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    harness.detectChanges();

    const emailField = root.querySelector<HTMLInputElement>('#email')!.parentElement!;
    expect(textOf(emailField)).toContain('This email is already registered.');
    expect(textOf(root)).toContain('This email is already registered.');
    expect(navigateByUrl).not.toHaveBeenCalled();
    expect(root.querySelector('button[type=submit]')?.hasAttribute('disabled')).toBe(false);
  });

  it('clears an earlier server error when the user submits again', () => {
    fill();
    submit();
    http.expectOne(`${api}/auth/register`).flush(
      { errors: { Email: ['This email is already registered.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    harness.detectChanges();

    typeInto(root, '#email', 'other@example.com');
    submit();

    expect(textOf(root)).not.toContain('This email is already registered.');
    http.expectOne(`${api}/auth/register`).flush(makeAuthResult(makeAccessToken(), 'refresh-1'));
  });
});