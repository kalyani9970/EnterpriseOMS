import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Guard factory: requirePermission('roles.view') returns a guard that allows the route only
 * when the user holds EVERY listed permission.
 *
 * This is a usability feature (hide pages the user cannot use), not a security boundary.
 * The API enforces the same permissions with [HasPermission] on every endpoint.
 */
export const requirePermission = (...permissions: string[]): CanActivateFn => {
  return (_route, state) => {
    const auth = inject(AuthService);
    const router = inject(Router);

    if (!auth.isAuthenticated()) {
      return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    }

    return permissions.every((p) => auth.hasPermission(p)) ? true : router.createUrlTree(['/forbidden']);
  };
};