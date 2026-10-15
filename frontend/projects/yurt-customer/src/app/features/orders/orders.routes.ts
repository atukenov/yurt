import { Routes } from '@angular/router';

export const ORDER_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./orders.component').then((m) => m.OrdersComponent),
  },
  {
    path: 'active',
    loadComponent: () => import('./active-order.component').then((m) => m.ActiveOrderComponent),
  },
  {
    path: ':id',
    loadComponent: () => import('./order-detail.component').then((m) => m.OrderDetailComponent),
  },
];
