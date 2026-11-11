import { ToOrderLine } from 'shared-models';
import { invoiceTotal, orderLineText, orderText, packsFor, whatsappLink } from './purchasing-helpers';

describe('purchasing helpers', () => {
  it('makes a WhatsApp link, turning a Kazakh 8 number into +7', () => {
    expect(whatsappLink('8 (701) 123-45-67', 'Hi')).toBe('https://wa.me/77011234567?text=Hi');
    expect(whatsappLink('+7 701 123 45 67', 'a b')).toBe('https://wa.me/77011234567?text=a%20b');
    expect(whatsappLink(null, 'x')).toBe('https://wa.me/?text=x');
  });

  it('writes order lines with packs when the pack is known', () => {
    expect(orderLineText({ name: 'Oat milk', unit: 'ml', quantity: 12000, packs: 2, packName: '6 x 1 L' })).toBe(
      'Oat milk — 2 × 6 x 1 L (12000 ml)',
    );
    expect(orderLineText({ name: 'Lids', unit: 'pcs', quantity: 90.5 })).toBe('Lids — 90.5 pcs');
    expect(orderText('Hello', [{ name: 'Lids', unit: 'pcs', quantity: 90 }], 'Cafe')).toBe('Hello\n\n• Lids — 90 pcs\n\nCafe');
  });

  it('rounds a changed amount up to whole packs and prices it', () => {
    const line: ToOrderLine = {
      ingredientId: 'i', name: 'Oat', unit: 'ml', left: 0, lowStockAt: 1, alreadyOrdered: 0,
      suggested: 12000, packs: 2, packSize: 6000, packName: '6 L', estimatedCost: 14400,
    };
    expect(packsFor(line, 13000)).toEqual({ packs: 3, cost: 21600 });
    expect(packsFor(line, 6000)).toEqual({ packs: 1, cost: 7200 });
    expect(packsFor({ ...line, packSize: null }, 5)).toEqual({ packs: null, cost: null });
    expect(packsFor(line, 0)).toEqual({ packs: null, cost: null });
  });

  it('adds up only the known line totals', () => {
    expect(invoiceTotal([{ total: 100 }, { total: null }, { total: 50.5 }])).toBe(150.5);
  });
});
