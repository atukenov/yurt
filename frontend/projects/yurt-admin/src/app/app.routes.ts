import { Routes } from '@angular/router';
import { adminAuthGuard } from './guards/auth.guard';
import { superAdminGuard } from './guards/super-admin.guard';
import { cafeScreenGuard } from './guards/cafe-screen.guard';
import { ceoGuard } from './guards/ceo.guard';
import { moduleGuard } from 'shared-api';

export const routes: Routes = [
  { path: '', redirectTo: 'auth/login', pathMatch: 'full' },
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/admin-login.component').then((m) => m.AdminLoginComponent),
  },
  {
    // The order-ready TV: full screen, no menu. Staff sign in once on the TV's browser.
    path: 'tv',
    canActivate: [adminAuthGuard, cafeScreenGuard, moduleGuard('own_pos', '/orders')],
    loadComponent: () => import('./features/tv/order-ready-tv.component').then((m) => m.OrderReadyTvComponent),
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
            path: 'new-cafe',
            loadComponent: () =>
              import('./features/platform/cafe-wizard.component').then((m) => m.CafeWizardComponent),
          },
          {
            path: 'go-live',
            loadComponent: () =>
              import('./features/platform/go-live.component').then((m) => m.GoLiveComponent),
          },
          {
            path: 'app-releases',
            loadComponent: () =>
              import('./features/app-releases/app-releases.component').then((m) => m.AppReleasesComponent),
          },
        ],
      },
      {
        // Screens inside one cafe. The platform operator is sent to /platform instead.
        path: '',
        canActivateChild: [cafeScreenGuard],
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
            path: 'printers',
            canActivate: [moduleGuard('own_pos', '/orders')],
            loadComponent: () =>
              import('./features/printers/printers.component').then((m) => m.PrintersComponent),
          },
          {
            path: 'staff-watch',
            canActivate: [moduleGuard('staff_watch', '/orders')],
            loadComponent: () =>
              import('./features/staff-watch/staff-watch.component').then((m) => m.StaffWatchComponent),
          },
          {
            path: 'tables',
            canActivate: [moduleGuard('table_ordering', '/orders')],
            loadComponent: () => import('./features/tables/tables.component').then((m) => m.TablesComponent),
          },
          {
            // Write-offs and moves between branches: open to staff, not only admins.
            path: 'stock-moves',
            canActivate: [moduleGuard('own_pos', '/orders')],
            loadComponent: () => import('./features/stock/stock-moves.component').then((m) => m.StockMovesComponent),
          },
          {
            // Suppliers, orders and delivery invoices: staff type in deliveries, admins run suppliers and payments.
            path: 'purchasing',
            canActivate: [moduleGuard('own_pos', '/orders')],
            loadComponent: () =>
              import('./features/purchasing/purchasing.component').then((m) => m.PurchasingComponent),
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
            path: 'combos',
            loadComponent: () => import('./features/combos/combos.component').then((m) => m.CombosComponent),
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
            canActivate: [ceoGuard, moduleGuard('analytics', '/orders')],
            loadComponent: () =>
              import('./features/analytics/analytics.component').then((m) => m.AnalyticsComponent),
          },
          {
            path: 'profit',
            canActivate: [ceoGuard, moduleGuard('analytics', '/orders')],
            loadComponent: () => import('./features/profit/profit.component').then((m) => m.ProfitComponent),
          },
          {
            path: 'accounting',
            canActivate: [ceoGuard, moduleGuard('analytics', '/orders')],
            loadComponent: () => import('./features/accounting/accounting.component').then((m) => m.AccountingComponent),
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
            path: 'manager-pin',
            canActivate: [moduleGuard('own_pos', '/orders')],
            loadComponent: () =>
              import('./features/manager-pin/manager-pin.component').then((m) => m.ManagerPinComponent),
          },
          {
            path: 'workers',
            canActivate: [moduleGuard('staff_audit', '/orders')],
            loadComponent: () =>
              import('./features/workers/workers.component').then((m) => m.WorkersComponent),
          },
          {
            path: 'audit-log',
            canActivate: [ceoGuard, moduleGuard('staff_audit', '/orders')],
            loadComponent: () =>
              import('./features/audit-log/audit-log.component').then((m) => m.AuditLogComponent),
          },
          {
            path: 'tips',
            canActivate: [ceoGuard, moduleGuard('own_pos', '/orders')],
            loadComponent: () => import('./features/tips/tips.component').then((m) => m.TipsComponent),
          },
          {
            path: 'payroll',
            canActivate: [ceoGuard, moduleGuard('payroll', '/orders')],
            loadComponent: () => import('./features/payroll/payroll.component').then((m) => m.PayrollComponent),
          },
          {
            path: 'subscription',
            canActivate: [ceoGuard],
            loadComponent: () =>
              import('./features/subscription/subscription.component').then((m) => m.SubscriptionComponent),
          },
          {
            path: 'guest-messages',
            canActivate: [moduleGuard('marketing', '/orders')],
            loadComponent: () =>
              import('./features/guest-messages/guest-messages.component').then((m) => m.GuestMessagesComponent),
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
    ],
  },
  { path: '**', redirectTo: 'auth/login' },
];
