import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { PromoPopupComponent } from './promo-popup.component';
import { TranslatePipe } from '../core/translate.pipe';
import { CartService } from '../features/cart/cart.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, PromoPopupComponent, TranslatePipe],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.css',
})
export class ShellComponent {
  readonly cart = inject(CartService);
}
