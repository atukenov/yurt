import { MenuItem } from 'shared-models';
import { formProblem, regularTotal } from './combos.component';

const dish = (id: string, price: number, variants: { id: string; price: number }[] = []): MenuItem => ({
  id, categoryId: 'c', categoryName: 'C', name: id, description: '', price, isAvailable: true, isNew: false,
  variants: variants.map((v, i) => ({ id: v.id, label: v.id, price: v.price, sortOrder: i, isDefault: i === 0 })),
});

describe('combo form', () => {
  const items = [dish('latte', 1000, [{ id: 'small', price: 900 }, { id: 'big', price: 1300 }]), dish('bun', 400)];

  it('adds up the separate prices, with the size picked', () => {
    expect(regularTotal([{ itemId: 'latte', variantId: 'big', quantity: 1 }, { itemId: 'bun', variantId: null, quantity: 2 }], items)).toBe(2100);
    expect(regularTotal([{ itemId: 'latte', variantId: null, quantity: 1 }], items)).toBe(1000);
    expect(regularTotal([{ itemId: 'gone', variantId: null, quantity: 3 }], items)).toBe(0);
  });

  it('asks for a name, a category, a price and at least two pieces', () => {
    const two = [{ itemId: 'latte', variantId: null, quantity: 1 }, { itemId: 'bun', variantId: null, quantity: 1 }];
    expect(formProblem('', 1000, 'c', two)).toBe('combos.errName');
    expect(formProblem('Combo', 1000, '', two)).toBe('combos.errCategory');
    expect(formProblem('Combo', null, 'c', two)).toBe('combos.errPrice');
    expect(formProblem('Combo', 1000, 'c', [{ itemId: 'latte', variantId: null, quantity: 1 }])).toBe('combos.errParts');
    expect(formProblem('Combo', 1000, 'c', [{ itemId: '', variantId: null, quantity: 1 }, ...two.slice(0, 1)])).toBe('combos.errParts');
    expect(formProblem('Combo', 1000, 'c', [{ itemId: 'latte', variantId: null, quantity: 2 }])).toBeNull();
    expect(formProblem('Combo', 1000, 'c', two)).toBeNull();
  });
});
