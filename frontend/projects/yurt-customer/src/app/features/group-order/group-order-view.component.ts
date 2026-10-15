import { Component, inject, Input, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';
import { YurtApiService, SignalrService, AuthStateService } from 'shared-api';
import { Currency2Pipe, ToastService } from 'shared-ui';
import { AddGroupOrderItemRequest, GroupCart, GroupCartItem, GroupCartStatus, MenuItem } from 'shared-models';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-group-order-view',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, Currency2Pipe],
  templateUrl: './group-order-view.component.html',
})
export class GroupOrderViewComponent implements OnInit, OnDestroy {
  @Input() id!: string;

  private api = inject(YurtApiService);
  private signalr = inject(SignalrService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private destroy$ = new Subject<void>();

  cart = signal<GroupCart | null>(null);
  loading = signal(true);
  checkoutLoading = signal(false);
  showAddSheet = signal(false);
  menuItems = signal<MenuItem[]>([]);
  menuLoading = signal(false);
  menuSearch = '';
  copySuccess = signal(false);

  readonly GroupCartStatus = GroupCartStatus;

  get myUserId(): string {
    return this.auth.currentUser?.userId ?? '';
  }

  get filteredItems(): MenuItem[] {
    const s = this.menuSearch.toLowerCase();
    return s ? this.menuItems().filter(i => i.name.toLowerCase().includes(s)) : this.menuItems();
  }

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.signalr.configure(environment.apiUrl);
    this.load();

    this.signalr.groupCartUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe((dto) => {
        if (dto.id === this.id) this.cart.set(dto);
      });

    this.signalr.startConnection().then(() => {
      this.signalr.joinGroupCartRoom(this.id);
    });
  }

  ngOnDestroy(): void {
    this.signalr.leaveGroupCartRoom(this.id);
    this.destroy$.next();
    this.destroy$.complete();
  }

  private load(): void {
    this.api.getGroupOrder(this.id).subscribe({
      next: (c) => { this.cart.set(c); this.loading.set(false); },
      error: () => { this.loading.set(false); },
    });
  }

  copyCode(): void {
    const code = this.cart()?.code;
    if (!code) return;
    navigator.clipboard.writeText(code).then(() => {
      this.copySuccess.set(true);
      setTimeout(() => this.copySuccess.set(false), 2000);
    });
  }

  openAddSheet(): void {
    this.showAddSheet.set(true);
    if (!this.menuItems().length) {
      this.menuLoading.set(true);
      const locationId = this.cart()?.locationId;
      this.api.getMenuItems(undefined, undefined, 'ru').subscribe({
        next: (items) => { this.menuItems.set(items); this.menuLoading.set(false); },
        error: () => this.menuLoading.set(false),
      });
    }
  }

  addItem(item: MenuItem): void {
    const req: AddGroupOrderItemRequest = {
      menuItemId: item.id,
      menuItemName: item.name,
      unitPrice: item.price,
      quantity: 1,
      toppings: [],
    };
    this.api.addGroupOrderItem(this.id, req).subscribe({
      next: (updated) => { this.cart.set(updated); this.showAddSheet.set(false); },
      error: () => {},
    });
  }

  removeItem(cartItem: GroupCartItem): void {
    this.api.removeGroupOrderItem(this.id, cartItem.id).subscribe({
      next: (updated) => this.cart.set(updated),
      error: () => {},
    });
  }

  placeOrder(): void {
    this.checkoutLoading.set(true);
    this.api.checkoutGroupOrder(this.id).subscribe({
      next: (order) => {
        this.toast.success('Order placed! ☕');
        this.router.navigate(['/orders', order.id]);
      },
      error: () => this.checkoutLoading.set(false),
    });
  }

  total(): number {
    return this.cart()?.items.reduce((sum, i) => sum + i.unitPrice * i.quantity, 0) ?? 0;
  }
}
