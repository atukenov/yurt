import { Routes } from '@angular/router';

export const MENU_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./menu-list.component').then((m) => m.MenuListComponent),
  },
  {
    path: 'item/:id',
    loadComponent: () => import('./item-detail.component').then((m) => m.ItemDetailComponent),
  },
];
