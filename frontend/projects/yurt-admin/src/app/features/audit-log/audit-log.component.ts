import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { AuditLogEntry } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

const PAGE_SIZE = 50;
const ENTITY_TYPES = ['', 'Order', 'MenuItem', 'MenuCategory', 'MenuTopping', 'AdminUser', 'Location', 'Promotion'];

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe],
  templateUrl: './audit-log.component.html',
})
export class AuditLogComponent implements OnInit {
  private api = inject(YurtApiService);

  entries = signal<AuditLogEntry[]>([]);
  loading = signal(true);
  total = signal(0);
  page = signal(1);

  entityTypeFilter = '';
  entityTypes = ENTITY_TYPES;

  get hasMore(): boolean {
    return this.entries().length < this.total();
  }

  ngOnInit(): void {
    this.load();
  }

  load(append = false): void {
    this.loading.set(true);
    this.api.getAuditLog({
      entityType: this.entityTypeFilter || undefined,
      page: this.page(),
      pageSize: PAGE_SIZE,
    }).subscribe({
      next: (res) => {
        this.total.set(res.total);
        this.entries.update(prev => append ? [...prev, ...res.items] : res.items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  applyFilter(): void {
    this.page.set(1);
    this.load();
  }

  loadMore(): void {
    this.page.update(p => p + 1);
    this.load(true);
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleString();
  }
}
