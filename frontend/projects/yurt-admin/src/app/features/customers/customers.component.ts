import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { Currency2Pipe } from 'shared-ui';
import { CustomerSummary } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-customers',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, Currency2Pipe, AdminTranslatePipe],
  templateUrl: './customers.component.html',
})
export class CustomersComponent implements OnInit {
  private api = inject(YurtApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  customers = signal<CustomerSummary[]>([]);
  loading = signal(true);
  phone = '';
  total = signal(0);
  page = signal(1);
  readonly pageSize = 50;

  ngOnInit(): void {
    this.load();
  }

  load(page = 1): void {
    this.page.set(page);
    this.loading.set(true);
    this.api.getAdminCustomers(this.phone || undefined, page, this.pageSize).subscribe({
      next: (res) => {
        const items = Array.isArray(res) ? (res as unknown as CustomerSummary[]) : res.items ?? [];
        const total = Array.isArray(res) ? res.length : res.total ?? 0;
        this.customers.set(items);
        this.total.set(total);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onSearch(): void {
    this.load(1);
  }

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize);
  }

  goToCustomer(id: string): void {
    this.router.navigate([id], { relativeTo: this.route });
  }
}
