import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { requirePermission } from './core/guards/permission.guard';
import { Permissions } from './core/security/permissions';

/**
 * Angular concept: loadComponent lazy-loads each screen, so the browser downloads a page's
 * code only when the user first visits it.
 */
export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    title: 'Create account',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register),
  },
  {
    // Everything inside the shell requires a signed-in user.
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'home' },
      {
        path: 'home',
        title: 'Home',
        loadComponent: () => import('./features/home/home').then((m) => m.Home),
      },
      {
        path: 'roles',
        title: 'Roles',
        canActivate: [requirePermission(Permissions.Roles.View)],
        loadComponent: () => import('./features/roles/roles').then((m) => m.Roles),
      },
      {
        path: 'forbidden',
        title: 'Access denied',
        loadComponent: () => import('./features/forbidden/forbidden').then((m) => m.Forbidden),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];