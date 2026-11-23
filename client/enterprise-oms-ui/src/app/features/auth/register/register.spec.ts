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

  type Field = 'organizationName' | 'fullName' | 'email' | 'password' | 'confirmPassword';

  function fill(overrides: Partial<Record<Field, string>> = {}): void {
    typeInto(root, '#organizationName', overrides.organizationName ?? 'Acme Traders');
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

  it('explains that signing up creates a new organization', () => {
    expect(textOf(root)).toContain('Create your organization');
    expect(root.querySelector('#organizationId')).toBeNull();   // the old temporary field is gone
    expect(root.querySelector('#organizationName')).not.toBeNull();
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
    submit();

    expect(textOf(root)).toContain('Organization name is required.');
    expect(textOf(root)).toContain('Full name is required.');
    expect(textOf(root)).toContain('Email is required.');
    expect(textOf(root)).toContain('Password is required.');
    http.expectNone(`${api}/auth/register`);
  });

  it('rejects an organization name made only of spaces', () => {
    fill({ organizationName: '     ' });

    submit();

    expect(textOf(root)).toContain('Organization name is required.');
    http.expectNone(`${api}/auth/register`);
  });

  it('rejects an organization name shorter than 2 characters', () => {
    fill({ organizationName: 'A' });

    submit();

    expect(textOf(root)).toContain('Organization name must be at least 2 characters.');
    http.expectNone(`${api}/auth/register`);
  });

  it('rejects an organization name longer than 200 characters', () => {
    fill({ organizationName: 'x'.repeat(201) });

    submit();

    expect(textOf(root)).toContain('Organization name must be 200 characters or fewer.');
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

  it('posts the organization and account details (trimmed, without the confirmation) and goes home', () => {
    fill({ organizationName: '  Acme Traders  ' });

    submit();

    const request = http.expectOne(`${api}/auth/register`);
    expect(request.request.body).toEqual({
      organizationName: 'Acme Traders',
      fullName: 'Jane Doe',
      email: 'jane@example.com',
      password: 'Passw0rd1',
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

  it('shows a server error for the organization name under that field', () => {
    fill();

    submit();
    http.expectOne(`${api}/auth/register`).flush(
      { errors: { OrganizationName: ['Organization name must be at least 2 characters.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    harness.detectChanges();

    const field = root.querySelector<HTMLInputElement>('#organizationName')!.parentElement!;
    expect(textOf(field)).toContain('Organization name must be at least 2 characters.');
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