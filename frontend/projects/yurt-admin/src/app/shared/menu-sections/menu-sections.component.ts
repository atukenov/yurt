import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/**
 * The bar at the top of the menu screens: Dishes, Combos and Time menus are one place,
 * so they are one sidebar entry ("Menu") and switch here.
 */
@Component({
  selector: 'app-menu-sections',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, AdminTranslatePipe],
  template: `
    <nav class="flex gap-1 bg-slate-100 rounded-xl p-1 w-fit max-w-full overflow-x-auto" [attr.aria-label]="'nav.menu' | t">
      @for (s of sections; track s.link) {
        <a [routerLink]="s.link" routerLinkActive="bg-surface text-slate-800 shadow-sm" #rla="routerLinkActive"
          [attr.aria-current]="rla.isActive ? 'page' : null"
          class="whitespace-nowrap px-4 py-1.5 rounded-lg text-sm font-medium text-slate-600 hover:text-slate-800 transition-all">{{ s.label | t }}</a>
      }
    </nav>
  `,
})
export class MenuSectionsComponent {
  readonly sections = [
    { link: '/menu', label: 'menu.sections.dishes' },
    { link: '/combos', label: 'nav.combos' },
    { link: '/time-menus', label: 'nav.timeMenus' },
  ];
}
