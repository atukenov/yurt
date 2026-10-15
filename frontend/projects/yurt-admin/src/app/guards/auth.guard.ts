import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthStateService } from 'shared-api';

export const adminAuthGuard: CanActivateChildFn = () => {
  const auth = inject(AuthStateService);
  const router = inject(Router);
  if (auth.isLoggedIn && auth.currentUser?.userType === 'admin') return true;
  return router.createUrlTree(['/auth/login']);
};
