import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { API_BASE_URL } from '../core/config/api-config';
import { textOf } from '../core/testing/dom-helpers';
import { makeAccessToken, makeAuthResult } from '../core/testing/make-jwt';
import { AuthService } from '../core/services/auth.service';
import { Shell } from './shell';

describe('Shell', () => {
  let http: HttpTestingController;
  let api: string;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(API_BASE_URL);
  });

  function signIn(permissions: string[]): void {
    TestBed.inject(AuthService).login({ email: 'jane@example.com', password: 'Passw0rd1' }).subscribe();
    http.expectOne(`${api}/auth/login`).flush(makeAuthResult(makeAccessToken({ permission: permissions }), 'refresh-1'));
  }

  async function render() {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  const navLinks = (root: HTMLElement) =>
    Array.from(root.querySelectorAll('nav a')).map((a) => a.textContent?.trim());

  it('shows the signed-in user name and role', async () => {
    signIn(['users.view']);

    const text = textOf(await render());

    expect(text).toContain('Jane Doe');
    expect(text).toContain('Admin');
  });

  it('shows the Roles link only to users who hold roles.view', async () => {
    signIn(['roles.view']);
    expect(navLinks(await render())).toEqual(['Home', 'Roles']);
  });

  it('hides the Roles link from users without roles.view', async () => {
    signIn(['users.view']);
    expect(navLinks(await render())).toEqual(['Home']);
  });

  it('signs out when the button is clicked', async () => {
    signIn(['users.view']);
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const root = await render();

    const signOut = Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Sign out'))!;
    signOut.click();

    http.expectOne(`${api}/auth/logout`).flush(null, { status: 204, statusText: 'No Content' });
    expect(TestBed.inject(AuthService).isAuthenticated()).toBe(false);
  });
});