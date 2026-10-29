import { HttpInterceptorFn } from '@angular/common/http';
import { inject, InjectionToken } from '@angular/core';
import { AuthStateService } from './auth-state.service';

/**
 * Key of the cafe a branded app belongs to (for example 'mycafe'). Provide it from the app's environment; leave it
 * unset for web apps whose address already identifies the cafe, and for the first cafe.
 */
export const TENANT_SLUG = new InjectionToken<string | null>('TENANT_SLUG', { providedIn: 'root', factory: () => null });

/**
 * Tells the backend which cafe the request is for: the cafe a native app belongs to (its page address says nothing
 * about it), else the cafe of the signed-in admin (one admin panel address serves every cafe). A cafe the request
 * already names (the operator's cafe switch) wins.
 */
export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const slug = inject(TENANT_SLUG) ?? inject(AuthStateService).cafe;
  return slug && req.url.includes('/api/v1/') && !req.headers.has('X-Tenant')
    ? next(req.clone({ setHeaders: { 'X-Tenant': slug } }))
    : next(req);
};
