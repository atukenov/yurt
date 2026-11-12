import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AuthStateService } from 'shared-api';

/** The till needs a signed-in staff member (any role: worker, admin or CEO). */
const staffGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  if (auth.isLoggedIn && auth.currentUser?.userType === 'admin' && !auth.isSuperAdmin) return true;
  return inject(Router).createUrlTree(['/login']);
};

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent) },
  { path: '', canActivate: [staffGuard], loadComponent: () => import('./features/sell/sell.component').then((m) => m.SellComponent) },
  { path: '**', redirectTo: '' },
];
