import { ItemProfit } from 'shared-models';
import { changePercent, sortItems } from './profit.component';

function item(name: string, profit: number | null, perItem: number | null, foodCost: number | null, sold: number): ItemProfit {
  return {
    menuItemId: name, name, sold, revenue: sold * 100, cost: profit === null ? null : 1,
    profit, profitPerItem: perItem, foodCostPercent: foodCost, estimatedCount: 0, missingPrice: false,
  };
}

describe('profit report helpers', () => {
  const items = [
    item('cheap-many', 5000, 50, 30, 100),
    item('fancy', 3000, 300, 20, 10),
    item('no-recipe', null, null, null, 500),
  ];

  it('sorts by total profit, per item, food cost and sold, unknown cost last', () => {
    expect(sortItems(items, 'profit').map((i) => i.name)).toEqual(['cheap-many', 'fancy', 'no-recipe']);
    expect(sortItems(items, 'perItem').map((i) => i.name)).toEqual(['fancy', 'cheap-many', 'no-recipe']);
    expect(sortItems(items, 'foodCost').map((i) => i.name)).toEqual(['fancy', 'cheap-many', 'no-recipe']);
    expect(sortItems(items, 'sold').map((i) => i.name)).toEqual(['no-recipe', 'cheap-many', 'fancy']);
  });

  it('works out the change against last month', () => {
    expect(changePercent(150, 100)).toBe(50);
    expect(changePercent(-50, 100)).toBe(-150);
    expect(changePercent(100, 0)).toBeNull();
    expect(changePercent(50, -100)).toBe(150);
  });
});
