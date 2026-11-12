import { ApplicationConfig, inject, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor, httpErrorInterceptor, tenantInterceptor, TENANT_SLUG } from 'shared-api';
import { UI_LANG } from 'shared-ui';
import { environment } from '../environments/environment';
import { PosLangService } from './core/lang.service';
import { quietInterceptor } from './core/quiet.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    // Same order as the admin app: tokens are refreshed before an error toast is shown. The till's own calls stay quiet.
    provideHttpClient(withInterceptors([quietInterceptor, httpErrorInterceptor, authInterceptor, tenantInterceptor])),
    { provide: UI_LANG, useFactory: () => { const l = inject(PosLangService); return () => l.lang(); } },
    { provide: TENANT_SLUG, useValue: (environment as { tenant?: string }).tenant ?? null },
  ],
};
