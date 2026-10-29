import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { ToastService } from 'shared-ui';
import { LangService } from '../../core/lang.service';
import { TableService } from '../../core/table.service';

/**
 * Opened by the QR code on a table (/t/:code). Remembers the table and its branch, then shows the menu, so the next
 * order goes to this table.
 */
@Component({
  selector: 'app-table-link',
  standalone: true,
  template: `<div class="min-h-screen flex items-center justify-center" style="background:var(--surface-warm)">
    <span class="inline-block w-8 h-8 border-4 border-current border-t-transparent rounded-full animate-spin" style="color:var(--brand-500)"></span>
  </div>`,
})
export class TableLinkComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = inject(YurtApiService);
  private tables = inject(TableService);
  private toast = inject(ToastService);
  private lang = inject(LangService);

  ngOnInit(): void {
    const code = this.route.snapshot.paramMap.get('code') ?? '';
    this.api.lookupTable(code).subscribe({
      next: (table) => {
        this.tables.set(table);
        this.toast.success(this.lang.t('table.welcome', { table: table.label }));
        this.router.navigateByUrl('/menu', { replaceUrl: true });
      },
      error: () => {
        this.toast.warning(this.lang.t('table.notFound'));
        this.router.navigateByUrl('/menu', { replaceUrl: true });
      },
    });
  }
}
