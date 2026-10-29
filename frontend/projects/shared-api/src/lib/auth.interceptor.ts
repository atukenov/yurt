import { HttpInterceptorFn, HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, finalize, shareReplay, switchMap, tap, throwError, Observable } from 'rxjs';
import { AuthStateService } from './auth-state.service';
import { YurtApiService } from './yurt-api.service';

// Shared across all interceptor calls — only one refresh HTTP call runs at a time.
// All concurrent 401s subscribe to the same Observable and get the cached result.
let pendingRefresh$: Observable<{ accessToken: string; refreshToken: string }> | null = null;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStateService);
  const api = inject(YurtApiService);

  return next(addToken(req, auth.token)).pipe(
    catchError((err: HttpErrorResponse) => {
      // A token issued before the password change still says "must change password" (403).
      // The flag lives in the database, so a refresh gets a token that reflects it.
      const staleMustChange =
        err.status === 403 &&
        err.error?.title === 'Password change required.' &&
        !req.url.includes('/auth/change-password');
      if (
        (err.status !== 401 && !staleMustChange) ||
        req.url.includes('/auth/refresh') ||
        req.url.includes('/auth/logout')
      ) {
        return throwError(() => err);
      }

      const rt = auth.refreshToken;
      if (!rt) {
        auth.logout();
        return throwError(() => err);
      }

      if (!pendingRefresh$) {
        const isAdmin = auth.currentUser?.userType === 'admin';
        const source$ = isAdmin ? api.adminRefreshToken(rt) : api.refreshToken(rt);

        pendingRefresh$ = source$.pipe(
          tap(res => auth.updateTokens(res.accessToken, res.refreshToken)),
          shareReplay(1),
          finalize(() => { pendingRefresh$ = null; }),
        );
      }

      return pendingRefresh$.pipe(
        switchMap(res => next(addToken(req, res.accessToken))),
        catchError(refreshErr => {
          if (refreshErr.status === 401 || refreshErr.status === 403) {
            auth.logout();
          }
          return throwError(() => refreshErr);
        }),
      );
    }),
  );
};

function addToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}
