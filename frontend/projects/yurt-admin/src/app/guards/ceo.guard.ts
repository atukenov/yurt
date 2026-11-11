import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStateService } from 'shared-api';

/** UI convenience only: the money endpoints and payroll are enforced for the CEO on the server. */
export const ceoGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  return auth.isCeo ? true : inject(Router).createUrlTree(['/orders']);
};
