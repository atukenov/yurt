import { inject } from '@angular/core';
import { CanActivateChildFn, Router } from '@angular/router';
import { AuthStateService } from 'shared-api';

/**
 * Screens that work inside one cafe (orders, menu, stock, ...) are for that cafe's own staff. The platform operator
 * controls cafes from the Platform page, so they are sent there instead. UI convenience only: the server decides
 * what each role may read or change.
 */
export const cafeScreenGuard: CanActivateChildFn = () => {
  const auth = inject(AuthStateService);
  return auth.isSuperAdmin ? inject(Router).createUrlTree(['/platform']) : true;
};
