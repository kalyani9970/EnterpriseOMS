import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_BASE_URL } from '../../core/config/api-config';
import { textOf } from '../../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../../core/testing/make-jwt';
import { AuthService } from '../../core/services/auth.service';
import { Home } from './home';

describe('Home', () => {
  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  function signIn(permissions: string[]): void {
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);
    const api = TestBed.inject(API_BASE_URL);

    auth.login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  async function render(): Promise<HTMLElement> {
    const fixture = TestBed.createComponent(Home);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  it('greets the user and shows role, email and organization', async () => {
    signIn(['users.view']);

    const text = textOf(await render());

    expect(text).toContain('Welcome, Jane Doe');
    expect(text).toContain('Admin');
    expect(text).toContain('jane@example.com');
    expect(text).toContain('#1');
  });

  it('groups permissions by module in alphabetical order', async () => {
    signIn(['users.view', 'roles.view', 'users.create']);

    const root = await render();
    const headings = Array.from(root.querySelectorAll('h3')).map((h) => h.textContent?.trim());
    const chips = Array.from(root.querySelectorAll('li')).map((li) => li.textContent?.trim());

    expect(headings).toEqual(['roles', 'users']);
    expect(chips).toEqual(['roles.view', 'users.create', 'users.view']);
  });

  it('explains when the role has no permissions yet', async () => {
    signIn([]);

    expect(textOf(await render())).toContain('Your role has no permissions yet.');
  });

  it('renders nothing when nobody is signed in', async () => {
    expect(textOf(await render())).toBe('');
  });
});