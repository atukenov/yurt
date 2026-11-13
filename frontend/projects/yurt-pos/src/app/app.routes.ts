import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { AuthStateService, moduleGuard } from 'shared-api';
import { LoginComponent } from './features/login/login.component';
import { KioskComponent } from './features/kiosk/kiosk.component';
import { GuestComponent } from './features/guest/guest.component';
import { SellComponent } from './features/sell/sell.component';

/** The till needs a signed-in staff member (any role: worker, admin or CEO). */
const staffGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  if (auth.isLoggedIn && auth.currentUser?.userType === 'admin' && !auth.isSuperAdmin) return true;
  return inject(Router).createUrlTree(['/login']);
};

// All screens are in the main file (no lazy parts), so the saved copy of the app always has everything it needs offline.
export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  // The guest-facing screen for a second monitor: no sign-in, it only shows what the till sends it.
  { path: 'guest', component: GuestComponent },
  { path: '', canActivate: [staffGuard], component: SellComponent },
  // The self-order kiosk: a guest-facing screen on its own tablet (module kiosk), opened by staff from the till.
  { path: 'kiosk', canActivate: [staffGuard, moduleGuard('kiosk', '/')], component: KioskComponent },
  { path: '**', redirectTo: '' },
];
