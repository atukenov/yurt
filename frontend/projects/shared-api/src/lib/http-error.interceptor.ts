import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from 'shared-ui';

/**
 * Set on a request's HttpContext to fully opt out of this interceptor's default
 * toast — for components that show their own specific/inline error UI instead
 * (e.g. a field-level validation message) and don't want a generic toast on top.
 *
 * Usage: `this.http.get(url, { context: new HttpContext().set(SKIP_ERROR_TOAST, true) })`
 */
export const SKIP_ERROR_TOAST = new HttpContextToken<boolean>(() => false);

/**
 * Catch-all HTTP error handling: standardizes toast messaging by error category
 * (offline, 5xx, 4xx) and centralizes error logging. Complements — does not
 * replace — the 401/token-refresh handling in `auth.interceptor.ts`.
 *
 * 401s are intentionally skipped here and left entirely to `authInterceptor`,
 * which retries the request after a token refresh or logs the user out. This
 * interceptor must be registered *before* `authInterceptor` in `withInterceptors`
 * so `authInterceptor` runs closer to the backend and resolves 401s first; this
 * interceptor then only sees errors that weren't (or couldn't be) recovered.
 */
export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const toast = inject(ToastService);

  return next(req).pipe(
    catchError((err: unknown) => {
      if (err instanceof HttpErrorResponse) {
        logHttpError(req, err);

        if (err.status !== 401 && !req.context.get(SKIP_ERROR_TOAST)) {
          toast.error(describeError(err));
        }
      }
      return throwError(() => err);
    }),
  );
};

// Both apps keep <html lang> set to the language the person picked, so this
// shared code can answer in it without knowing about either app's lang service.
const MESSAGES: Record<'en' | 'ru' | 'kk', Record<'offline' | 'server' | 'forbidden' | 'failed', string>> = {
  en: {
    offline: 'You appear to be offline. Check your connection and try again.',
    server: 'Something went wrong on our end. Please try again in a moment.',
    forbidden: "You don't have permission to do that.",
    failed: 'Request failed ({status}). Please try again.',
  },
  ru: {
    offline: 'Похоже, нет интернета. Проверьте подключение и попробуйте ещё раз.',
    server: 'Что-то пошло не так на нашей стороне. Попробуйте ещё раз чуть позже.',
    forbidden: 'У вас нет прав на это действие.',
    failed: 'Запрос не выполнен ({status}). Попробуйте ещё раз.',
  },
  kk: {
    offline: 'Интернет жоқ сияқты. Байланысты тексеріп, қайталап көріңіз.',
    server: 'Біз жақта бірдеңе дұрыс болмады. Сәлден соң қайталап көріңіз.',
    forbidden: 'Бұл әрекетке рұқсатыңыз жоқ.',
    failed: 'Сұраныс орындалмады ({status}). Қайталап көріңіз.',
  },
};

export function errorMessage(key: keyof (typeof MESSAGES)['en'], lang = document.documentElement.lang): string {
  return (MESSAGES[lang as keyof typeof MESSAGES] ?? MESSAGES.en)[key];
}

function describeError(err: HttpErrorResponse): string {
  // ASP.NET's ProblemDetails convention (used throughout this API) puts the
  // user-facing message in `title`, with `detail` for extra context.
  const backendMessage: string | undefined = err.error?.title ?? err.error?.detail ?? err.error?.message;
  if (backendMessage) return backendMessage;

  if (err.status === 0) {
    return errorMessage('offline');
  }
  if (err.status >= 500) {
    return errorMessage('server');
  }
  if (err.status === 403) {
    return errorMessage('forbidden');
  }
  return errorMessage('failed').replace('{status}', String(err.status));
}

// No error-reporting/telemetry sink exists in this codebase yet — this is the
// single place to wire one in (Sentry, App Insights, etc.) without touching
// every call site.
function logHttpError(req: HttpRequest<unknown>, err: HttpErrorResponse): void {
  console.error(`[HTTP ${err.status || 'ERR'}] ${req.method} ${req.url}`, err.error ?? err.message);
}
