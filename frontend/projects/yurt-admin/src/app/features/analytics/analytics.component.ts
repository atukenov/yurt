import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { AnalyticsResponse, Location } from 'shared-models';
import { environment } from '../../../environments/environment';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.css',
})
export class AnalyticsComponent implements OnInit {
  private api = inject(YurtApiService);

  data = signal<AnalyticsResponse | null>(null);
  loading = signal(true);
  activePeriod = signal('month');
  locations = signal<Location[]>([]);
  selectedLocationId = signal<string | null>(null);

  // Tap-to-reveal state for chart tooltips (hover alone doesn't work on touch devices).
  activeRevenuePoint = signal<number | null>(null);
  activeHour = signal<number | null>(null);

  private readonly trendCaptionKeys: Record<string, string> = {
    today: 'analytics.trendToday',
    week: 'analytics.trendWeek',
    month: 'analytics.trendMonth',
    '6months': 'analytics.trend6Months',
    year: 'analytics.trendYear',
  };

  readonly periods = [
    { label: 'Today', value: 'today' },
    { label: 'Week', value: 'week' },
    { label: 'Month', value: 'month' },
    { label: '6M', value: '6months' },
    { label: '1Y', value: 'year' },
    { label: 'All', value: 'all' },
  ];

  // ── Computed helpers for charts ──────────────────────────────────────────

  revenueMax = computed(() => {
    const d = this.data();
    if (!d || d.revenueOverTime.length === 0) return 1;
    return Math.max(...d.revenueOverTime.map((r) => r.revenue)) || 1;
  });

  topItemMax = computed(() => {
    const d = this.data();
    if (!d || d.topItems.length === 0) return 1;
    return d.topItems[0]?.quantitySold || 1;
  });

  locationMax = computed(() => {
    const d = this.data();
    if (!d || d.locationPerformance.length === 0) return 1;
    return Math.max(...d.locationPerformance.map((l) => l.revenue)) || 1;
  });

  hourlyMax = computed(() => {
    const d = this.data();
    if (!d || d.hourlyDistribution.length === 0) return 1;
    return Math.max(...d.hourlyDistribution.map((h) => h.orders)) || 1;
  });

  paymentTotal = computed(() => {
    const d = this.data();
    if (!d) return 1;
    return d.paymentBreakdown.reduce((sum, p) => sum + p.count, 0) || 1;
  });

  totalStatusCount = computed(() => {
    const d = this.data();
    if (!d) return 1;
    return d.statusBreakdown.reduce((sum, s) => sum + s.count, 0) || 1;
  });

  trendCaptionKey = computed(() => this.trendCaptionKeys[this.activePeriod()] ?? '');

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.api.getAdminLocations().subscribe({ next: (locs) => this.locations.set(locs), error: () => {} });
    this.loadData();
  }

  setPeriod(period: string): void {
    this.activePeriod.set(period);
    this.loadData();
  }

  setLocation(id: string | null): void {
    this.selectedLocationId.set(id || null);
    this.loadData();
  }

  private loadData(): void {
    this.loading.set(true);
    this.activeRevenuePoint.set(null);
    this.activeHour.set(null);
    this.api.getAnalytics(this.activePeriod(), this.selectedLocationId() ?? undefined).subscribe({
      next: (res) => {
        this.data.set(res);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  toggleRevenuePoint(index: number): void {
    this.activeRevenuePoint.set(this.activeRevenuePoint() === index ? null : index);
  }

  toggleHour(hour: number): void {
    this.activeHour.set(this.activeHour() === hour ? null : hour);
  }

  // Positive/negative framing differs per metric: rising prep time is bad, rising revenue is good.
  trendInfo(
    percent: number | null,
    lowerIsBetter = false,
  ): { text: string; colorClass: string; arrow: string } | null {
    if (percent === null) return null;
    const isFlat = Math.abs(percent) < 0.05;
    const isGood = isFlat ? null : lowerIsBetter ? percent < 0 : percent > 0;
    const sign = percent > 0 ? '+' : '';
    return {
      text: `${sign}${percent}%`,
      colorClass: isFlat ? 'text-slate-400' : isGood ? 'text-emerald-600' : 'text-red-500',
      arrow: isFlat ? '·' : percent > 0 ? '▲' : '▼',
    };
  }

  exportCsv(): void {
    const d = this.data();
    if (!d) return;
    const rows: string[][] = [
      ['Section', 'Label', 'Value 1', 'Value 2'],
      ['KPI', 'Total Revenue', String(d.kpis.totalRevenue), ''],
      ['KPI', 'Total Orders', String(d.kpis.totalOrders), ''],
      ['KPI', 'Avg Order Value', String(d.kpis.avgOrderValue), ''],
      ['KPI', 'Completed Orders', String(d.kpis.completedOrders), ''],
      ['KPI', 'Declined Orders', String(d.kpis.declinedOrders), ''],
      ['KPI', 'Unique Customers', String(d.kpis.uniqueCustomers), ''],
      ['KPI', 'Avg Prep Time (min)', String(d.kpis.avgPrepTimeMinutes), ''],
      ...d.revenueOverTime.map((r) => ['Revenue', r.label, String(r.revenue), String(r.orders)]),
      ...d.topItems.map((t) => ['Top Item', t.name, String(t.quantitySold), String(t.revenue)]),
      ...d.locationPerformance.map((l) => ['Location', l.locationName, String(l.revenue), String(l.orders)]),
    ];
    const csv = rows.map((r) => r.map((c) => `"${c}"`).join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `analytics-${this.activePeriod()}-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  barHeight(value: number, max: number): string {
    if (max === 0) return '0%';
    return `${Math.max((value / max) * 100, 2)}%`;
  }

  barWidth(value: number, max: number): string {
    if (max === 0) return '0%';
    return `${Math.max((value / max) * 100, 2)}%`;
  }

  formatHour(hour: number): string {
    if (hour === 0) return '12a';
    if (hour < 12) return `${hour}a`;
    if (hour === 12) return '12p';
    return `${hour - 12}p`;
  }

  formatTenge(value: number): string {
    const rounded = Math.round(value);
    return '₸' + rounded.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
  }

  statusColor(status: string): string {
    const map: Record<string, string> = {
      Created: 'bg-amber-100 text-amber-700',
      Accepted: 'bg-blue-100 text-blue-700',
      Preparing: 'bg-teal-100 text-teal-700',
      Ready: 'bg-green-100 text-green-700',
      Completed: 'bg-emerald-100 text-emerald-700',
      Declined: 'bg-red-100 text-red-700',
    };
    return map[status] ?? 'bg-slate-100 text-slate-700';
  }

  paymentColor(index: number): string {
    const colors = ['bg-amber-500', 'bg-blue-500', 'bg-teal-500', 'bg-rose-400'];
    return colors[index % colors.length];
  }
}
