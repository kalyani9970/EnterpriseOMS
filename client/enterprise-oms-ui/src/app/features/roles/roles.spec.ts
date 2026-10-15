import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../core/config/api-config';
import { Role } from '../../core/models/role.models';
import { textOf } from '../../core/testing/dom-helpers';
import { Roles } from './roles';

describe('Roles', () => {
  let http: HttpTestingController;
  let api: string;
  let fixture: ComponentFixture<Roles>;

  const roles: Role[] = [
    { id: 1, name: 'Admin', description: 'Full access to all modules.', isSystemRole: true, permissions: ['users.view', 'roles.view'] },
    { id: 2, name: 'Employee', description: null, isSystemRole: true, permissions: [] },
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
    fixture = TestBed.createComponent(Roles);
  });

  afterEach(() => http.verify());

  const root = () => fixture.nativeElement as HTMLElement;

  it('shows a loading state, then the roles', async () => {
    fixture.detectChanges();
    expect(textOf(root())).toContain('Loading roles…');

    http.expectOne(`${api}/roles`).flush(roles);
    await fixture.whenStable();

    const text = textOf(root());
    expect(text).toContain('Admin');
    expect(text).toContain('Full access to all modules.');
    expect(text).toContain('2 permissions');
    expect(text).toContain('users.view');
    expect(text).toContain('Employee');
    expect(text).toContain('0 permissions');
    expect(text).not.toContain('Loading roles…');
  });

  it('shows an empty message when there are no roles', async () => {
    fixture.detectChanges();

    http.expectOne(`${api}/roles`).flush([]);
    await fixture.whenStable();

    expect(textOf(root())).toContain('No roles found.');
  });

  it('shows the server error and retries on demand', async () => {
    fixture.detectChanges();

    http.expectOne(`${api}/roles`).flush({}, { status: 403, statusText: 'Forbidden' });
    await fixture.whenStable();

    expect(textOf(root())).toContain('You do not have permission to do that.');

    root().querySelector<HTMLButtonElement>('button')!.click();
    await fixture.whenStable();
    expect(textOf(root())).toContain('Loading roles…');

    http.expectOne(`${api}/roles`).flush(roles);
    await fixture.whenStable();
    expect(textOf(root())).toContain('Admin');
  });
});