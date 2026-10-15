import { Routes } from '@angular/router';
import { adminAuthGuard } from './guards/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'auth/login', pathMatch: 'full' },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/admin-login.component').then((m) => m.AdminLoginComponent),
  },
  {
    path: 'auth/change-password',
    loadComponent: () =>
      import('./features/auth/change-password.component').then((m) => m.ChangePasswordComponent),
  },
  {
    path: '',
    canActivateChild: [adminAuthGuard],
    loadComponent: () =>
      import('./layout/admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      {
        path: 'orders',
        loadComponent: () =>
          import('./features/orders/orders-live.component').then((m) => m.OrdersLiveComponent),
      },
      {
        path: 'menu',
        loadComponent: () =>
          import('./features/menu/menu-management.component').then(
            (m) => m.MenuManagementComponent,
          ),
      },
      {
        path: 'locations',
        loadComponent: () =>
          import('./features/locations/locations-management.component').then(
            (m) => m.LocationsManagementComponent,
          ),
      },
      {
        path: 'analytics',
        loadComponent: () =>
          import('./features/analytics/analytics.component').then((m) => m.AnalyticsComponent),
      },
      {
        path: 'promotions',
        loadComponent: () =>
          import('./features/promotions/promotions-management.component').then(
            (m) => m.PromotionsManagementComponent,
          ),
      },
      { path: 'dashboard', redirectTo: 'orders', pathMatch: 'full' },
      {
        path: 'customers',
        loadComponent: () =>
          import('./features/customers/customers.component').then((m) => m.CustomersComponent),
      },
      {
        path: 'customers/:id',
        loadComponent: () =>
          import('./features/customers/customer-detail.component').then((m) => m.CustomerDetailComponent),
      },
      {
        path: 'workers',
        loadComponent: () =>
          import('./features/workers/workers.component').then((m) => m.WorkersComponent),
      },
      {
        path: 'audit-log',
        loadComponent: () =>
          import('./features/audit-log/audit-log.component').then((m) => m.AuditLogComponent),
      },
      {
        path: 'app-releases',
        loadComponent: () =>
          import('./features/app-releases/app-releases.component').then((m) => m.AppReleasesComponent),
      },
      {
        path: 'discount-codes',
        loadComponent: () =>
          import('./features/discount-codes/discount-codes.component').then((m) => m.DiscountCodesComponent),
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports.component').then((m) => m.ReportsComponent),
      },
      { path: '', redirectTo: 'orders', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'auth/login' },
];
