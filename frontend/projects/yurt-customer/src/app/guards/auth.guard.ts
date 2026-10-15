import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { toObservable } from '@angular/core/rxjs-interop';
import { filter, map, take } from 'rxjs';
import { AuthStateService } from 'shared-api';
import { AppStateService } from '../core/app-state.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthStateService);
  const appState = inject(AppStateService);
  const router = inject(Router);

  const checkAuth = () => {
    if (auth.isLoggedIn) return true;
    return router.createUrlTree(['/auth/login']);
  };

  if (appState.refreshReady()) return checkAuth();

  return toObservable(appState.refreshReady).pipe(
    filter(Boolean),
    take(1),
    map(() => checkAuth()),
  );
};
