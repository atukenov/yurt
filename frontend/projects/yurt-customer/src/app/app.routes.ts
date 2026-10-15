import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';
import { noAuthGuard } from './guards/no-auth.guard';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'menu',
    pathMatch: 'full',
  },
  {
    path: 'auth',
    canActivateChild: [noAuthGuard],
    loadChildren: () => import('./features/auth/auth.routes').then((m) => m.AUTH_ROUTES),
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    children: [
      {
        path: 'locations',
        loadComponent: () =>
          import('./features/locations/locations.component').then((m) => m.LocationsComponent),
      },
      {
        path: 'menu',
        loadChildren: () => import('./features/menu/menu.routes').then((m) => m.MENU_ROUTES),
      },
      {
        path: 'cart',
        canActivate: [authGuard],
        loadComponent: () => import('./features/cart/cart.component').then((m) => m.CartComponent),
      },
      {
        path: 'orders',
        canActivate: [authGuard],
        loadChildren: () => import('./features/orders/orders.routes').then((m) => m.ORDER_ROUTES),
      },
      {
        path: 'favorites',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/favorites/favorites.component').then((m) => m.FavoritesComponent),
      },
      {
        path: 'profile',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/profile/profile.component').then((m) => m.ProfileComponent),
      },
      {
        path: 'achievements',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/achievements/achievements.component').then((m) => m.AchievementsComponent),
      },
      {
        path: 'news',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/news/news.component').then((m) => m.NewsComponent),
      },
      {
        path: 'group-order/create',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/group-order/group-order-create.component').then((m) => m.GroupOrderCreateComponent),
      },
      {
        path: 'group-order/join',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/group-order/group-order-join.component').then((m) => m.GroupOrderJoinComponent),
      },
      {
        path: 'group-order/:id',
        canActivate: [authGuard],
        loadComponent: () =>
          import('./features/group-order/group-order-view.component').then((m) => m.GroupOrderViewComponent),
      },
    ],
  },
  { path: '**', redirectTo: 'menu' },
];
