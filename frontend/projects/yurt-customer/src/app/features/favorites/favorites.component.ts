import { Component, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { MenuItem } from 'shared-models';
import { ToastService, Currency2Pipe, SkeletonCardComponent } from 'shared-ui';
import { CartService } from '../cart/cart.service';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { PullToRefreshDirective } from '../../shared/pull-to-refresh.directive';

@Component({
  selector: 'app-favorites',
  standalone: true,
  imports: [CommonModule, RouterLink, Currency2Pipe, SkeletonCardComponent, TranslatePipe, PullToRefreshDirective],
  templateUrl: './favorites.component.html',
  styleUrl: './favorites.component.css',
})
export class FavoritesComponent {
  private api = inject(YurtApiService);
  private cart = inject(CartService);
  private toast = inject(ToastService);
  private langService = inject(LangService);

  loading = signal(true);
  items = signal<MenuItem[]>([]);

  constructor() {
    effect(() => {
      const lang = this.langService.lang();
      this.loadFavorites(lang);
    });
  }

  loadFavorites(lang?: string): void {
    this.loading.set(true);
    this.api.getFavorites(lang ?? this.langService.lang()).subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  addToCart(item: MenuItem): void {
    const added = this.cart.addItem({
      menuItemId: item.id,
      name: item.name,
      price: item.price,
      quantity: 1,
      imageUrl: item.imageUrl,
    });
    if (added) this.toast.success(`${item.name} added to cart`);
  }

  removeFavorite(item: MenuItem): void {
    this.api.removeFavorite(item.id).subscribe(() => {
      this.items.update((list) => list.filter((i) => i.id !== item.id));
      this.toast.info('Removed from favorites');
    });
  }
}
