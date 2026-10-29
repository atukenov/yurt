import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthStateService, YurtApiService } from 'shared-api';
import { AuditLogEntry, Cafe } from 'shared-models';
import { LocalDatePipe } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';

const PAGE_SIZE = 50;
const ENTITY_TYPES = ['', 'Order', 'MenuItem', 'MenuCategory', 'MenuTopping', 'AdminUser', 'Location', 'Promotion'];

@Component({
  selector: 'app-audit-log',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe, LocalDatePipe],
  templateUrl: './audit-log.component.html',
})
export class AuditLogComponent implements OnInit {
  private api = inject(YurtApiService);
  /** The platform operator sees the platform's own actions, or picks one cafe; a cafe admin only ever sees their cafe. */
  readonly isOperator = inject(AuthStateService).isSuperAdmin;
  cafes = signal<Cafe[]>([]);
  /** '' = platform actions, else the slug of the cafe whose history is shown. */
  cafeFilter = '';

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
    if (this.isOperator) this.api.getCafes().subscribe({ next: (c) => this.cafes.set(c), error: () => this.cafes.set([]) });
    this.load();
  }

  load(append = false): void {
    this.loading.set(true);
    this.api.getAuditLog({
      cafe: this.isOperator ? this.cafeFilter || undefined : undefined,
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
}
