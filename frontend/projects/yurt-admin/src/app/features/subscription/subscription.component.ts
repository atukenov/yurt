import { LocalDatePipe } from 'shared-ui';
import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { CafeAccess, ModuleStatusInfo } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-subscription',
  standalone: true,
  imports: [LocalDatePipe, CommonModule, AdminTranslatePipe],
  templateUrl: './subscription.component.html',
})
export class SubscriptionComponent implements OnInit {
  private api = inject(YurtApiService);

  modules = signal<ModuleStatusInfo[]>([]);
  access = signal<CafeAccess | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.api.getAdminSubscription().subscribe({
      next: (s) => { this.modules.set(s.modules); this.access.set(s.access ?? null); this.loading.set(false); },
      error: () => this.loading.set(false),
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
