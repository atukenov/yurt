import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { MaintenanceInfo } from 'shared-models';
import { YurtApiService } from './yurt-api.service';

const CACHE_KEY = 'yurt.modules';

/**
 * Which paid modules are switched on. Fail-open by design: until the config has loaded, when the
 * request fails, or when the server predates the endpoint, every module counts as enabled so a
 * lookup problem can never lock a cafe's customers out.
 *
 * The last known list is cached in localStorage and applied immediately (stale-while-revalidate).
 */
@Injectable({ providedIn: 'root' })
export class ModuleService {
  private api = inject(YurtApiService);

  /** Null means "unknown": everything is treated as enabled. */
  private readonly enabledKeys = signal<ReadonlySet<string> | null>(this.readCache());

  /** Set while the operator has maintenance mode on; the apps show a notice. Never persisted. */
  readonly maintenance = signal<MaintenanceInfo | null>(null);

  /** Reads the cache synchronously, then refreshes from the server. Safe to call more than once. */
  load(): void {
    this.api.getAppConfig().subscribe({
      next: (cfg) => {
        this.enabledKeys.set(new Set(cfg.modules));
        this.writeCache(cfg.modules);
        this.maintenance.set(cfg.maintenance?.enabled ? cfg.maintenance : null);
      },
      error: () => { /* keep the cached/unknown state: fail-open */ },
    });
  }

  isEnabled(key: string): boolean {
    const keys = this.enabledKeys();
    return keys === null || keys.has(key);
  }

  private readCache(): ReadonlySet<string> | null {
    try {
      const raw = localStorage.getItem(CACHE_KEY);
      if (!raw) return null;
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? new Set<string>(parsed.filter((k) => typeof k === 'string')) : null;
    } catch {
      return null;
    }
  }

  private writeCache(keys: string[]): void {
    try {
      localStorage.setItem(CACHE_KEY, JSON.stringify(keys));
    } catch { /* storage unavailable */ }
  }
}

/** Route guard: sends the user to `redirectTo` when the module is off. Enabled when unknown. */
export function moduleGuard(key: string, redirectTo = '/'): CanActivateFn {
  return () => {
    const modules = inject(ModuleService);
    return modules.isEnabled(key) ? true : inject(Router).parseUrl(redirectTo);
  };
}
