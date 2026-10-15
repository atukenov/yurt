import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor, httpErrorInterceptor } from 'shared-api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // httpErrorInterceptor first (outermost) so authInterceptor — closest to the
    // backend — resolves 401s via token refresh before an unrecovered error ever
    // reaches httpErrorInterceptor's default toast.
    provideHttpClient(withInterceptors([httpErrorInterceptor, authInterceptor])),
  ],
};
