import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { UserReport } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, AdminTranslatePipe],
  templateUrl: './reports.component.html',
})
export class ReportsComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  tab = signal<'open' | 'resolved'>('open');
  reports = signal<UserReport[]>([]);
  loading = signal(true);
  resolvingId = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const resolved = this.tab() === 'resolved';
    this.api.getAdminReports(resolved).subscribe({
      next: (r) => { this.reports.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  setTab(tab: 'open' | 'resolved'): void {
    this.tab.set(tab);
    this.load();
  }

  resolve(report: UserReport): void {
    this.resolvingId.set(report.id);
    this.api.resolveReport(report.id).subscribe({
      next: () => {
        this.resolvingId.set(null);
        this.reports.update((list) => list.filter((r) => r.id !== report.id));
        this.toast.success('Report marked as resolved.');
      },
      error: () => this.resolvingId.set(null),
    });
  }
}
