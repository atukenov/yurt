import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Ingredient, MenuItem, MenuTopping, RecipeLine } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/**
 * The recipe of one menu item: how much of each ingredient a portion uses (Phase 6, `own_pos`). A line can apply to
 * every size or to one size only. Saving replaces the whole recipe.
 */
@Component({
  selector: 'app-recipes-panel',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe],
  templateUrl: './recipes-panel.component.html',
})
export class RecipesPanelComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  items = signal<MenuItem[]>([]);
  toppings = signal<MenuTopping[]>([]);
  ingredients = signal<Ingredient[]>([]);
  lines = signal<RecipeLine[]>([]);
  saving = signal(false);
  /** Either a menu item id, or `t:` plus a topping id. */
  menuItemId = '';
  dirty = false;

  ngOnInit(): void {
    this.api.adminGetMenuItems().subscribe((items) => this.items.set(items));
    this.api.adminGetToppings().subscribe((list) => this.toppings.set(list));
    // The branch only decides the counts, which this panel does not show, so any branch works for the name list.
    this.api.getAdminLocations().subscribe((locs) => {
      if (locs[0])
        this.api.getIngredients(locs[0].id).subscribe((list) => this.ingredients.set(list));
    });
  }

  get isTopping(): boolean {
    return this.menuItemId.startsWith('t:');
  }

  private get targetId(): string {
    return this.isTopping ? this.menuItemId.slice(2) : this.menuItemId;
  }

  /** Sizes only exist for dishes; a topping uses the same amount whatever the size. */
  get variants() {
    return this.isTopping
      ? []
      : (this.items().find((i) => i.id === this.menuItemId)?.variants ?? []);
  }

  pick(): void {
    this.dirty = false;
    this.lines.set([]);
    if (!this.menuItemId) return;
    const load = this.isTopping
      ? this.api.getToppingUses(this.targetId)
      : this.api.getRecipe(this.targetId);
    load.subscribe((lines) => this.lines.set(lines));
  }

  addLine(): void {
    const first = this.ingredients()[0];
    if (!first) return;
    this.lines.update((l) => [...l, { ingredientId: first.id, variantId: null, quantity: 1 }]);
    this.dirty = true;
  }

  removeLine(index: number): void {
    this.lines.update((l) => l.filter((_, i) => i !== index));
    this.dirty = true;
  }

  unitOf(ingredientId: string): string {
    return this.ingredients().find((i) => i.id === ingredientId)?.unit ?? '';
  }

  save(): void {
    this.saving.set(true);
    const save = this.isTopping
      ? this.api.saveToppingUses(this.targetId, this.lines())
      : this.api.saveRecipe(this.targetId, this.lines());
    save.subscribe({
      next: (lines) => {
        this.lines.set(lines);
        this.dirty = false;
        this.saving.set(false);
        this.toast.success('Saved');
      },
      error: () => {
        this.saving.set(false);
        this.toast.error('Could not save: check the amounts and sizes');
      },
    });
  }
}
