import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { Location } from 'shared-models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-group-order-create',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './group-order-create.component.html',
})
export class GroupOrderCreateComponent implements OnInit {
  private api = inject(YurtApiService);
  private router = inject(Router);

  locations = signal<Location[]>([]);
  selectedId = signal<string | null>(null);
  loading = signal(false);
  locLoading = signal(true);

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.api.getLocations().subscribe({
      next: (locs) => { this.locations.set(locs.filter(l => l.isActive)); this.locLoading.set(false); },
      error: () => this.locLoading.set(false),
    });
  }

  create(): void {
    const id = this.selectedId();
    if (!id) return;
    this.loading.set(true);
    this.api.createGroupOrder(id).subscribe({
      next: (cart) => this.router.navigate(['/group-order', cart.id]),
      error: () => this.loading.set(false),
    });
  }
}
