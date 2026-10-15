import { Injectable, computed, inject, signal } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { App } from '@capacitor/app';
import { AppUpdateInfo } from 'shared-models';
import { YurtApiService } from 'shared-api';
import { LangService } from './lang.service';

function compareVersions(a: string, b: string): number {
  const pa = a.split('.').map((n) => parseInt(n, 10) || 0);
  const pb = b.split('.').map((n) => parseInt(n, 10) || 0);
  const len = Math.max(pa.length, pb.length);
  for (let i = 0; i < len; i++) {
    const diff = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

/**
 * Blocks the app behind an "update required" gate when the running native build
 * is older than the minimum version the backend reports for its platform.
 * A blank minimum version (the default) leaves the gate disabled, and any
 * failure to determine the local or required version fails open — this must
 * never trap a user who simply has no network connection.
 */
@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly api = inject(YurtApiService);

  private readonly lang = inject(LangService);
  private static readonly DISMISSED_KEY = 'yurt_update_dismissed';

  /** Blocking gate: the running build is below the minimum supported version. */
  readonly updateRequired = signal(false);
  /** Dismissible "what's new" sheet: a newer release exists but the app still works. */
  readonly updateAvailable = signal(false);
  readonly storeUrl = signal('');
  readonly currentVersion = signal('');
  readonly latestVersion = signal('');
  private readonly rawNotes = signal<Pick<AppUpdateInfo, 'latestNotesEn' | 'latestNotesRu' | 'latestNotesKk'>>({});

  /** Release notes in the user's language, one entry per line. */
  readonly notes = computed(() => {
    const n = this.rawNotes();
    const l = this.lang.lang();
    const text = (l === 'ru' ? n.latestNotesRu : l === 'kk' ? n.latestNotesKk : n.latestNotesEn) || n.latestNotesEn || '';
    return text.split('\n').map((x) => x.trim()).filter(Boolean);
  });

  readonly visible = computed(() => this.updateRequired() || this.updateAvailable());

  dismiss(): void {
    if (this.updateRequired()) return;
    try {
      localStorage.setItem(AppUpdateService.DISMISSED_KEY, this.latestVersion());
    } catch { /* storage unavailable — popup just shows again next launch */ }
    this.updateAvailable.set(false);
  }

  private dismissedVersion(): string | null {
    try {
      return localStorage.getItem(AppUpdateService.DISMISSED_KEY);
    } catch {
      return null;
    }
  }

  async check(): Promise<void> {
    if (!Capacitor.isNativePlatform()) return;

    let currentVersion: string;
    try {
      currentVersion = (await App.getInfo()).version;
    } catch {
      return;
    }

    const platform = Capacitor.getPlatform();
    this.api.getAppUpdateInfo().subscribe({
      next: (info) => {
        const minVersion = platform === 'ios' ? info.minVersionIos : info.minVersionAndroid;
        const storeUrl = platform === 'ios' ? info.storeUrlIos : info.storeUrlAndroid;
        if (!storeUrl) return;

        this.currentVersion.set(currentVersion);
        this.latestVersion.set(info.latestVersion ?? '');
        this.rawNotes.set(info);
        this.storeUrl.set(storeUrl);

        if (minVersion && compareVersions(currentVersion, minVersion) < 0) {
          this.updateRequired.set(true);
          return;
        }

        const latest = info.latestVersion;
        if (latest && compareVersions(currentVersion, latest) < 0 && this.dismissedVersion() !== latest) {
          this.updateAvailable.set(true);
        }
      },
      error: () => {},
    });
  }
}
