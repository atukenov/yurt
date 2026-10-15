import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStateService } from 'shared-api';

/** UI convenience only: every /platform endpoint is enforced for SuperAdmin on the server. */
export const superAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  return auth.isSuperAdmin ? true : inject(Router).createUrlTree(['/orders']);
};
