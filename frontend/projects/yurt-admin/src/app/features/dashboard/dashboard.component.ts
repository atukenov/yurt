import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { Currency2Pipe } from 'shared-ui';
import { DashboardData } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, Currency2Pipe, AdminTranslatePipe],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  private api = inject(YurtApiService);

  data = signal<DashboardData | null>(null);
  loading = signal(true);
  error = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.api.getDashboard().subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set(true); },
    });
  }

  chartBars(): { hour: string; hourNum: number; count: number; pct: number }[] {
    const d = this.data();
    if (!d?.hourlyOrders) return [];
    const maxCount = Math.max(...d.hourlyOrders.map((h) => h.count), 1);
    return d.hourlyOrders.map((h) => ({
      hour: h.hour.toString().padStart(2, '0') + ':00',
      hourNum: h.hour,
      count: h.count,
      pct: Math.round((h.count / maxCount) * 100),
    }));
  }
}
