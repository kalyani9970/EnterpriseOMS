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
    title: 'Create organization',
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
        path: 'products',
        title: 'Products',
        canActivate: [requirePermission(Permissions.Products.View)],
        loadComponent: () => import('./features/products/product-list/product-list').then((m) => m.ProductList),
      },
      {
        // Must come BEFORE 'products/:id', or "new" would be read as a product id.
        path: 'products/new',
        title: 'New product',
        canActivate: [requirePermission(Permissions.Products.Create)],
        loadComponent: () => import('./features/products/product-form/product-form').then((m) => m.ProductForm),
      },
      {
        path: 'products/:id',
        title: 'Product',
        canActivate: [requirePermission(Permissions.Products.View)],
        loadComponent: () => import('./features/products/product-form/product-form').then((m) => m.ProductForm),
      },
      {
        path: 'warehouses',
        title: 'Warehouses',
        canActivate: [requirePermission(Permissions.Warehouses.View)],
        loadComponent: () =>
          import('./features/warehouses/warehouse-list/warehouse-list').then((m) => m.WarehouseList),
      },
      {
        // Must come BEFORE 'warehouses/:id', or "new" would be read as a warehouse id.
        path: 'warehouses/new',
        title: 'New warehouse',
        canActivate: [requirePermission(Permissions.Warehouses.Create)],
        loadComponent: () =>
          import('./features/warehouses/warehouse-form/warehouse-form').then((m) => m.WarehouseForm),
      },
      {
        path: 'warehouses/:id',
        title: 'Warehouse',
        canActivate: [requirePermission(Permissions.Warehouses.View)],
        loadComponent: () =>
          import('./features/warehouses/warehouse-form/warehouse-form').then((m) => m.WarehouseForm),
      },
      {
        path: 'organization',
        title: 'Organization',
        canActivate: [requirePermission(Permissions.Organizations.View)],
        loadComponent: () =>
          import('./features/organization/organization-settings').then((m) => m.OrganizationSettings),
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