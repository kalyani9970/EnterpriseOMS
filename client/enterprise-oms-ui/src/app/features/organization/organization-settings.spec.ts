import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../core/config/api-config';
import { Organization } from '../../core/models/organization.models';
import { AuthService } from '../../core/services/auth.service';
import { submitForm, textOf, typeInto } from '../../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../../core/testing/make-jwt';
import { OrganizationSettings } from './organization-settings';

describe('OrganizationSettings', () => {
  let http: HttpTestingController;
  let api: string;
  let fixture: ComponentFixture<OrganizationSettings>;

  const organization: Organization = {
    id: 3,
    name: 'Acme Traders',
    code: 'acme-traders',
    isActive: true,
    created: '2026-10-17T09:30:00Z',
    userCount: 4,
  };

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  afterEach(() => http.verify());

  function signIn(permissions: string[]): void {
    TestBed.inject(AuthService).login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  /** Creates the page and answers its initial GET. */
  async function open(permissions: string[], loaded: Organization = organization): Promise<HTMLElement> {
    signIn(permissions);

    fixture = TestBed.createComponent(OrganizationSettings);
    fixture.detectChanges();

    http.expectOne(`${api}/organizations/current`).flush(loaded);
    await fixture.whenStable();

    return fixture.nativeElement as HTMLElement;
  }

  const saveButton = (root: HTMLElement) => root.querySelector<HTMLButtonElement>('button[type=submit]')!;

  it('shows a loading state, then the organization details', async () => {
    signIn(['organizations.view']);
    fixture = TestBed.createComponent(OrganizationSettings);
    fixture.detectChanges();
    expect(textOf(fixture.nativeElement)).toContain('Loading organization…');

    http.expectOne(`${api}/organizations/current`).flush(organization);
    await fixture.whenStable();

    const text = textOf(fixture.nativeElement);
    expect(text).toContain('Acme Traders');
    expect(text).toContain('acme-traders');
    expect(text).toContain('4');
    expect(text).toContain('Active');
    expect(text).not.toContain('Loading organization…');
  });

  it('marks a deactivated organization as Inactive', async () => {
    const root = await open(['organizations.view'], { ...organization, isActive: false });

    expect(textOf(root)).toContain('Inactive');
  });

  it('is read-only for users without organizations.update', async () => {
    const root = await open(['organizations.view']);

    expect(root.querySelector('form')).toBeNull();
    expect(textOf(root)).toContain('You can view the organization but not change it.');
  });

  it('shows the rename form, prefilled, for users with organizations.update', async () => {
    const root = await open(['organizations.view', 'organizations.update']);

    expect(root.querySelector<HTMLInputElement>('#name')?.value).toBe('Acme Traders');
    expect(textOf(root)).toContain('The code acme-traders stays the same.');
  });

  it('disables Save while the name is unchanged, and enables it after an edit', async () => {
    const root = await open(['organizations.view', 'organizations.update']);
    expect(saveButton(root).disabled).toBe(true);

    typeInto(root, '#name', 'Acme Holdings');
    fixture.detectChanges();
    expect(saveButton(root).disabled).toBe(false);

    typeInto(root, '#name', '  Acme Traders  ');
    fixture.detectChanges();
    expect(saveButton(root).disabled).toBe(true);
  });

  it('saves the trimmed name, shows the result and confirms', async () => {
    const root = await open(['organizations.view', 'organizations.update']);

    typeInto(root, '#name', '  Acme Holdings  ');
    submitForm(root);
    fixture.detectChanges();

    const request = http.expectOne(`${api}/organizations/current`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'Acme Holdings' });
    expect(saveButton(root).disabled).toBe(true);                       // busy while saving
    expect(textOf(root)).toContain('Saving…');

    request.flush({ ...organization, name: 'Acme Holdings' });
    await fixture.whenStable();

    const text = textOf(root);
    expect(text).toContain('Saved.');
    expect(root.querySelector('h2')?.textContent).toContain('Acme Holdings');   // the card updated
    expect(root.querySelector<HTMLInputElement>('#name')?.value).toBe('Acme Holdings');
    expect(saveButton(root).disabled).toBe(true);                       // nothing left to save
  });

  it('rejects an empty name without calling the API', async () => {
    const root = await open(['organizations.view', 'organizations.update']);

    typeInto(root, '#name', '   ');
    submitForm(root);
    fixture.detectChanges();

    expect(textOf(root)).toContain('Organization name is required.');
    http.expectNone(`${api}/organizations/current`);
  });

  it('rejects a one-character name without calling the API', async () => {
    const root = await open(['organizations.view', 'organizations.update']);

    typeInto(root, '#name', 'A');
    submitForm(root);
    fixture.detectChanges();

    expect(textOf(root)).toContain('Organization name must be at least 2 characters.');
    http.expectNone(`${api}/organizations/current`);
  });

  it('shows a server error and lets the user try again', async () => {
    const root = await open(['organizations.view', 'organizations.update']);

    typeInto(root, '#name', 'Acme Holdings');
    submitForm(root);
    fixture.detectChanges();
    http.expectOne(`${api}/organizations/current`).flush(
      { title: 'One or more validation errors occurred.', errors: { Name: ['Organization name is required.'] } },
      { status: 400, statusText: 'Bad Request' },
    );
    await fixture.whenStable();

    expect(textOf(root)).toContain('Organization name is required.');
    expect(textOf(root)).not.toContain('Saved.');
    expect(saveButton(root).disabled).toBe(false);
    expect(root.querySelector<HTMLInputElement>('#name')?.value).toBe('Acme Holdings');   // the edit is kept
  });

  it('shows the load error and retries on demand', async () => {
    signIn(['organizations.view']);
    fixture = TestBed.createComponent(OrganizationSettings);
    fixture.detectChanges();

    http.expectOne(`${api}/organizations/current`).flush({}, { status: 403, statusText: 'Forbidden' });
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;

    expect(textOf(root)).toContain('You do not have permission to do that.');

    root.querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(textOf(root)).toContain('Loading organization…');

    http.expectOne(`${api}/organizations/current`).flush(organization);
    await fixture.whenStable();
    expect(textOf(root)).toContain('Acme Traders');
  });
});