import { goesBelowZero, MovableItem } from './stock-moves.component';
import { countedValue } from './stocktake-panel.component';

describe('stock pack helpers', () => {
  const milk: MovableItem = { kind: 'ingredient', id: 'm', name: 'Milk', unit: 'ml', left: 300 };

  it('warns only when taking more than is left', () => {
    expect(goesBelowZero(milk, 300)).toBe(false);
    expect(goesBelowZero(milk, 300.5)).toBe(true);
    expect(goesBelowZero(milk, null)).toBe(false);
    expect(goesBelowZero(null, 10)).toBe(false);
  });

  it('treats an empty stocktake line as skipped, and keeps zero as a real count', () => {
    expect(countedValue(null)).toBeNull();
    expect(countedValue('' as unknown as number)).toBeNull();
    expect(countedValue(-1)).toBeNull();
    expect(countedValue(0)).toBe(0);
    expect(countedValue(12.5)).toBe(12.5);
  });
});
