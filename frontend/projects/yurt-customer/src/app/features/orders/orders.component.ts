import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { YurtApiService } from 'shared-api';
import { OffsitePurchase, Order, OrderStatus } from 'shared-models';
import {
  BadgeComponent,
  OrderStatusLabelPipe,
  OrderStatusColorPipe,
  Currency2Pipe,
  SkeletonOrderCardComponent,
} from 'shared-ui';
import { TranslatePipe } from '../../core/translate.pipe';
import { AppResumeService } from '../../core/app-resume.service';
import { PullToRefreshDirective } from '../../shared/pull-to-refresh.directive';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    BadgeComponent,
    OrderStatusLabelPipe,
    OrderStatusColorPipe,
    Currency2Pipe,
    SkeletonOrderCardComponent,
    TranslatePipe,
    PullToRefreshDirective,
  ],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.css',
})
export class OrdersComponent implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private appResume = inject(AppResumeService);
  private resumeSub?: Subscription;

  loading = signal(true);
  activeOrders = signal<Order[]>([]);
  historyOrders = signal<Order[]>([]);
  declinedOrders = signal<Order[]>([]);
  offsiteTransactions = signal<OffsitePurchase[]>([]);
  activeTab = signal(0);

  readonly tabs = [
    { id: 0, labelKey: 'orders.tabActive' },
    { id: 1, labelKey: 'orders.tabHistory' },
    { id: 2, labelKey: 'orders.tabDeclined' },
  ];

  get visibleOrders(): () => Order[] {
    return () => {
      switch (this.activeTab()) {
        case 0:
          return this.activeOrders();
        case 1:
          return this.historyOrders();
        case 2:
          return this.declinedOrders();
        default:
          return [];
      }
    };
  }

  ngOnInit(): void {
    this.resumeSub = this.appResume.resumed$.subscribe(() => this.loadOrders());
    this.loadOrders();
  }

  loadOrders(): void {
    Promise.all([
      this.api.getActiveOrders().toPromise(),
      this.api.getOrderHistory().toPromise(),
      this.api.getDeclinedOrders().toPromise(),
    ])
      .then(([active, history, declined]) => {
        this.activeOrders.set(active ?? []);
        this.historyOrders.set(history ?? []);
        this.declinedOrders.set(declined ?? []);
        this.loading.set(false);
      })
      .catch(() => this.loading.set(false));

    // Independent of order loading: iiko offsite (in-shop counter) bonus history is a
    // nice-to-have on top of the History tab, not core order data — a failure here must
    // never surface as an "orders failed to load" error.
    this.api
      .getLoyaltyTransactions()
      .toPromise()
      .then((history) => this.offsiteTransactions.set(history?.transactions ?? []))
      .catch(() => this.offsiteTransactions.set([]));
  }

  ngOnDestroy(): void {
    this.resumeSub?.unsubscribe();
  }
}