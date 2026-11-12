import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AuthStateService } from 'shared-api';
import { LoginComponent } from './features/login/login.component';
import { SellComponent } from './features/sell/sell.component';

/** The till needs a signed-in staff member (any role: worker, admin or CEO). */
const staffGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  if (auth.isLoggedIn && auth.currentUser?.userType === 'admin' && !auth.isSuperAdmin) return true;
  return inject(Router).createUrlTree(['/login']);
};

// Both screens are in the main file (no lazy parts), so the saved copy of the app always has everything it needs offline.
export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: '', canActivate: [staffGuard], component: SellComponent },
  { path: '**', redirectTo: '' },
];
