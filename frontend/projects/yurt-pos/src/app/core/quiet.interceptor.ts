import { HttpInterceptorFn } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from 'shared-api';

/**
 * The till handles its own failures: reading the menu or branches falls back to the copy saved on the tablet, and a
 * sale that can't be sent is kept and uploaded later. So those calls never show the red "no internet" pop-ups, which
 * would only scare the cashier while the till keeps working.
 */
export const quietInterceptor: HttpInterceptorFn = (req, next) => {
  const quiet = req.method === 'GET' || req.url.endsWith('/admin/counter-sales') || req.url.endsWith('/admin/kiosk-orders');
  return next(quiet ? req.clone({ context: req.context.set(SKIP_ERROR_TOAST, true) }) : req);
};
