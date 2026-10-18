import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Ingredient, MenuItem, RecipeLine } from 'shared-models';
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
  ingredients = signal<Ingredient[]>([]);
  lines = signal<RecipeLine[]>([]);
  saving = signal(false);
  menuItemId = '';
  dirty = false;

  ngOnInit(): void {
    this.api.adminGetMenuItems().subscribe((items) => this.items.set(items));
    // The branch only decides the counts, which this panel does not show, so any branch works for the name list.
    this.api.getAdminLocations().subscribe((locs) => {
      if (locs[0])
        this.api.getIngredients(locs[0].id).subscribe((list) => this.ingredients.set(list));
    });
  }

  get variants() {
    return this.items().find((i) => i.id === this.menuItemId)?.variants ?? [];
  }

  pick(): void {
    this.dirty = false;
    this.lines.set([]);
    if (!this.menuItemId) return;
    this.api.getRecipe(this.menuItemId).subscribe((lines) => this.lines.set(lines));
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
    this.api.saveRecipe(this.menuItemId, this.lines()).subscribe({
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
