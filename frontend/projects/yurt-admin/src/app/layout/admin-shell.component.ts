import { Component, effect, inject, OnDestroy, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { CommonModule } from '@angular/common';
import {
  AuthStateService,
  BrandService,
  ModuleService,
  ThemeService,
  YurtApiService,
} from 'shared-api';
import { ToastService } from 'shared-ui';
import { AdminLangService } from '../core/lang.service';
import { AdminTranslatePipe } from '../core/translate.pipe';
import { BluetoothPrintService } from '../core/bluetooth/bluetooth-print.service';
import { buildAdminNav, NavGroup } from './admin-nav';

const SIDEBAR_KEY = 'yurt_admin_sidebar_collapsed';
const FOLDED_KEY = 'yurt_admin_nav_folded';

@Component({
  selector: 'app-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, AdminTranslatePipe],
  templateUrl: './admin-shell.component.html',
  styleUrl: './admin-shell.component.css',
})
export class AdminShellComponent implements OnDestroy {
  readonly auth = inject(AuthStateService);
  readonly modules = inject(ModuleService);
  readonly brand = inject(BrandService);
  readonly langService = inject(AdminLangService);
  readonly theme = inject(ThemeService);
  readonly themeModes = ['light', 'dark', 'system'] as const;
  private router = inject(Router);
  private toast = inject(ToastService);
  private api = inject(YurtApiService);
  private bluetooth = inject(BluetoothPrintService);

  /** Dishes and ingredients running low or out, for the badge on the Stock menu entry (own_pos). */
  stockAlerts = signal(0);
  private alertTimer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    effect(() => {
      if (this.alertTimer) {
        clearInterval(this.alertTimer);
        this.alertTimer = null;
      }
      // The platform operator does not work inside a cafe: no stock badge, no tablet printing.
      if (!this.modules.isEnabled('own_pos') || !this.auth.currentUser || this.auth.isSuperAdmin) {
        this.stockAlerts.set(0);
        this.bluetooth.stop();
        return;
      }
      // Bluetooth printers paired with this tablet print from any page while the app is open.
      this.bluetooth.start();
      this.refreshAlerts();
      this.alertTimer = setInterval(() => this.refreshAlerts(), 120_000);
    });
  }

  ngOnDestroy(): void {
    if (this.alertTimer) clearInterval(this.alertTimer);
    this.bluetooth.stop();
  }

  private refreshAlerts(): void {
    this.api.getStockAlerts().subscribe({
      next: (a) => this.stockAlerts.set(a.low + a.out),
      error: () => {},
    });
  }

  /** Sidebar groups for this person (role and modules decide what is in them). */
  navGroups(): NavGroup[] {
    return buildAdminNav({
      isAdmin: this.auth.isAdmin,
      isSuperAdmin: this.auth.isSuperAdmin,
      moduleEnabled: (key) => this.modules.isEnabled(key),
    });
  }

  private url = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => e.urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** Ids of the groups the person folded away, remembered on this device. */
  folded = signal<ReadonlySet<string>>(readFolded());

  /** A folded group still opens while one of its pages is showing, so the current page is never hidden. */
  groupOpen(group: NavGroup): boolean {
    if (!this.folded().has(group.id)) return true;
    const url = this.url().split(/[?#]/)[0];
    return group.items.some((i) => url === i.link || url.startsWith(i.link + '/'));
  }

  toggleGroup(group: NavGroup): void {
    const next = new Set(this.folded());
    if (this.groupOpen(group)) next.add(group.id);
    else next.delete(group.id);
    this.folded.set(next);
    try {
      localStorage.setItem(FOLDED_KEY, JSON.stringify([...next]));
    } catch {
      /* private mode: folding just is not remembered */
    }
  }

  collapsed = signal(localStorage.getItem(SIDEBAR_KEY) === '1' || window.innerWidth < 768);

  toggleSidebar(): void {
    const next = !this.collapsed();
    this.collapsed.set(next);
    localStorage.setItem(SIDEBAR_KEY, next ? '1' : '0');
  }

  initials(): string {
    const name = this.auth.currentUser?.displayName ?? '?';
    return name.slice(0, 2).toUpperCase();
  }

  logout(): void {
    this.bluetooth.stop();
    this.auth.logout();
    this.toast.info(this.langService.t('auth.signedOut'));
    this.router.navigate(['/auth/login']);
  }
}

function readFolded(): ReadonlySet<string> {
  try {
    const ids = JSON.parse(localStorage.getItem(FOLDED_KEY) ?? '[]');
    return new Set(Array.isArray(ids) ? ids.filter((x): x is string => typeof x === 'string') : []);
  } catch {
    return new Set();
  }
}
