import { ApplicationConfig, inject, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor, httpErrorInterceptor, tenantInterceptor, TENANT_SLUG } from 'shared-api';
import { UI_LANG } from 'shared-ui';
import { environment } from '../environments/environment';
import { AdminLangService } from './core/lang.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // httpErrorInterceptor first (outermost) so authInterceptor — closest to the
    // backend — resolves 401s via token refresh before an unrecovered error ever
    // reaches httpErrorInterceptor's default toast.
    provideHttpClient(withInterceptors([httpErrorInterceptor, authInterceptor, tenantInterceptor])),
    // Dates and numbers follow the language picked in the app, and switch with it.
    { provide: UI_LANG, useFactory: () => { const l = inject(AdminLangService); return () => l.lang(); } },
    { provide: TENANT_SLUG, useValue: (environment as { tenant?: string }).tenant ?? null },
  ],
};
