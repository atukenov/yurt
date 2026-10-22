import { Component, effect, inject, OnDestroy, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
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

const SIDEBAR_KEY = 'yurt_admin_sidebar_collapsed';

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
      if (!this.modules.isEnabled('own_pos') || !this.auth.currentUser) {
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
    this.toast.info('Signed out');
    this.router.navigate(['/auth/login']);
  }
}
