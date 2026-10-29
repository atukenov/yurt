import { LocalDatePipe } from 'shared-ui';
import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { ActivatedRoute } from '@angular/router';
import { CafeAccess, CafeBilling, ModuleStatusInfo } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-subscription',
  standalone: true,
  imports: [LocalDatePipe, CommonModule, AdminTranslatePipe],
  templateUrl: './subscription.component.html',
})
export class SubscriptionComponent implements OnInit {
  private api = inject(YurtApiService);

  private route = inject(ActivatedRoute);

  modules = signal<ModuleStatusInfo[]>([]);
  access = signal<CafeAccess | null>(null);
  loading = signal(true);
  billing = signal<CafeBilling | null>(null);
  /** Back from Stripe's checkout: the payment is confirmed by Stripe's webhook a few seconds later. */
  justPaid = signal(false);
  redirecting = signal(false);

  ngOnInit(): void {
    this.justPaid.set(this.route.snapshot.queryParamMap.get('billing') === 'done');
    this.load();
    // Give Stripe's webhook a moment, then show the new dates.
    if (this.justPaid()) setTimeout(() => this.load(), 5000);
  }

  private load(): void {
    this.api.getAdminSubscription().subscribe({
      next: (s) => { this.modules.set(s.modules); this.access.set(s.access ?? null); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.getCafeBilling().subscribe({ next: (b) => this.billing.set(b), error: () => this.billing.set(null) });
  }

  /** This page without query parameters; Stripe sends the owner back here. */
  private returnUrl(): string {
    return window.location.origin + window.location.pathname;
  }

  payByCard(): void {
    this.redirecting.set(true);
    this.api.startBillingCheckout(this.returnUrl()).subscribe({
      next: (r) => { window.location.href = r.url; },
      error: () => this.redirecting.set(false),
    });
  }

  manageBilling(): void {
    this.redirecting.set(true);
    this.api.openBillingPortal(this.returnUrl()).subscribe({
      next: (r) => { window.location.href = r.url; },
      error: () => this.redirecting.set(false),
    });
  }

  badgeClass(m: ModuleStatusInfo): string {
    switch (m.state) {
      case 'active': return 'bg-emerald-100 text-emerald-700';
      case 'grace': return 'bg-brand-50 text-brand-ink';
      default: return 'bg-slate-100 text-slate-600';
    }
  }
}
