import { Component, inject, OnInit, signal } from '@angular/core';
import { YurtApiService } from 'shared-api';
import { FoodCost } from 'shared-models';
import { Currency2Pipe, LocalNumberPipe } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/** Food cost % above this is flagged; coffee shops usually aim for 25-35 %. */
export const HIGH_FOOD_COST = 35;

/** What one portion of each dish costs to make from its recipe, against its price (stock pack, `own_pos`). */
@Component({
  selector: 'app-food-cost-panel',
  standalone: true,
  imports: [AdminTranslatePipe, Currency2Pipe, LocalNumberPipe],
  templateUrl: './food-cost-panel.component.html',
})
export class FoodCostPanelComponent implements OnInit {
  private api = inject(YurtApiService);

  rows = signal<FoodCost[]>([]);
  loading = signal(true);
  readonly high = HIGH_FOOD_COST;

  ngOnInit(): void {
    this.api.getFoodCost().subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  isHigh(row: FoodCost): boolean {
    return (row.foodCostPercent ?? 0) > HIGH_FOOD_COST;
  }
}
