import { HttpInterceptorFn } from '@angular/common/http';
import { inject, InjectionToken } from '@angular/core';

/**
 * Key of the cafe a branded app belongs to (for example 'mycafe'). Provide it from the app's environment; leave it
 * unset for web apps whose address already identifies the cafe, and for the first cafe.
 */
export const TENANT_SLUG = new InjectionToken<string | null>('TENANT_SLUG', { providedIn: 'root', factory: () => null });

/** Tells the backend which cafe a native app belongs to, since its page address says nothing about it. */
export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const slug = inject(TENANT_SLUG);
  return slug && req.url.includes('/api/v1/') ? next(req.clone({ setHeaders: { 'X-Tenant': slug } })) : next(req);
};
