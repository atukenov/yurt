import { Injectable } from '@angular/core';
import { Observable, defer, finalize, shareReplay, tap } from 'rxjs';

interface Entry<T> {
  at: number;
  data: T;
}

const PREFIX = 'yurt_swr:';
const MAX_AGE_MS = 7 * 24 * 60 * 60 * 1000;

/**
 * Stale-while-revalidate for public, read-only data (menu, categories, promotions, locations).
 * `swr()` emits the saved copy immediately (if any) and then the fresh network response, so
 * screens render instantly and quietly update. If the network fails but a saved copy was
 * shown, the error is swallowed; with nothing saved the error is passed on as usual.
 * Concurrent calls for the same key share one request.
 */
@Injectable({ providedIn: 'root' })
export class SwrCache {
  private readonly memory = new Map<string, Entry<unknown>>();
  private readonly inflight = new Map<string, Observable<unknown>>();

  swr<T>(key: string, fetch: () => Observable<T>): Observable<T> {
    return new Observable<T>((subscriber) => {
      const cached = this.read<T>(key);
      if (cached) subscriber.next(cached.data);

      let fresh = this.inflight.get(key) as Observable<T> | undefined;
      if (!fresh) {
        fresh = defer(fetch).pipe(
          tap((data) => this.write(key, data)),
          finalize(() => this.inflight.delete(key)),
          shareReplay(1),
        );
        this.inflight.set(key, fresh);
      }

      const sub = fresh.subscribe({
        next: (data) => {
          // Skip a redundant second emission when nothing changed
          if (!cached || JSON.stringify(cached.data) !== JSON.stringify(data)) subscriber.next(data);
        },
        error: (err) => (cached ? subscriber.complete() : subscriber.error(err)),
        complete: () => subscriber.complete(),
      });
      return () => sub.unsubscribe();
    });
  }

  /** Drop everything saved (e.g. on logout or from a "clear cache" action). */
  clear(): void {
    this.memory.clear();
    try {
      Object.keys(localStorage)
        .filter((k) => k.startsWith(PREFIX))
        .forEach((k) => localStorage.removeItem(k));
    } catch { /* storage unavailable */ }
  }

  private read<T>(key: string): Entry<T> | null {
    let entry = this.memory.get(key) as Entry<T> | undefined;
    if (!entry) {
      try {
        const raw = localStorage.getItem(PREFIX + key);
        if (raw) entry = JSON.parse(raw) as Entry<T>;
      } catch { /* corrupt or unavailable — treat as a miss */ }
    }
    if (!entry || Date.now() - entry.at > MAX_AGE_MS) return null;
    this.memory.set(key, entry);
    return entry;
  }

  private write<T>(key: string, data: T): void {
    const entry: Entry<T> = { at: Date.now(), data };
    this.memory.set(key, entry);
    try {
      localStorage.setItem(PREFIX + key, JSON.stringify(entry));
    } catch { /* quota exceeded or unavailable — memory copy still works this session */ }
  }
}
