import { Routes } from '@angular/router';
import { adminAuthGuard } from './guards/auth.guard';
import { superAdminGuard } from './guards/super-admin.guard';
import { moduleGuard } from 'shared-api';

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
        path: 'kitchen',
        canActivate: [moduleGuard('own_pos', '/orders')],
        loadComponent: () =>
          import('./features/kitchen/kitchen-display.component').then(
            (m) => m.KitchenDisplayComponent,
          ),
      },
      {
        path: 'cashier',
        canActivate: [moduleGuard('own_pos', '/orders')],
        loadComponent: () => import('./features/cashier/cashier.component').then((m) => m.CashierComponent),
      },
      {
        path: 'stock',
        canActivate: [moduleGuard('own_pos', '/orders')],
        loadComponent: () =>
          import('./features/stock/stock-management.component').then((m) => m.StockManagementComponent),
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
        canActivate: [moduleGuard('analytics', '/orders')],
        loadComponent: () =>
          import('./features/analytics/analytics.component').then((m) => m.AnalyticsComponent),
      },
      {
        path: 'promotions',
        canActivate: [moduleGuard('marketing', '/orders')],
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
        canActivate: [moduleGuard('staff_audit', '/orders')],
        loadComponent: () =>
          import('./features/workers/workers.component').then((m) => m.WorkersComponent),
      },
      {
        path: 'audit-log',
        canActivate: [moduleGuard('staff_audit', '/orders')],
        loadComponent: () =>
          import('./features/audit-log/audit-log.component').then((m) => m.AuditLogComponent),
      },
      { path: 'app-releases', redirectTo: 'platform/app-releases', pathMatch: 'full' },
      {
        path: 'platform',
        canActivate: [superAdminGuard],
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./features/platform/platform.component').then((m) => m.PlatformComponent),
          },
          {
            path: 'app-releases',
            loadComponent: () =>
              import('./features/app-releases/app-releases.component').then((m) => m.AppReleasesComponent),
          },
        ],
      },
      {
        path: 'subscription',
        loadComponent: () =>
          import('./features/subscription/subscription.component').then((m) => m.SubscriptionComponent),
      },
      {
        path: 'discount-codes',
        canActivate: [moduleGuard('marketing', '/orders')],
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
