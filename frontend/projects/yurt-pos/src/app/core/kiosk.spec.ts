import { CartLine } from './cart';
import {
  allergensOf, DONE_RESET_MS, EXIT_TAPS, EXIT_TAP_WINDOW_MS, IDLE_RESET_MS, IDLE_WARN_MS, idleState, orderNumber,
  registerExitTap, secondsToReset, toKioskRequest,
} from './kiosk';

const line = (over: Partial<CartLine> = {}): CartLine => ({
  key: 'k', menuItemId: 'item-1', name: 'Latte', variantId: 'v-1', variantLabel: 'L',
  toppings: [{ id: 't-1', name: 'Oat milk', price: 3 }], unitPrice: 13, quantity: 2, ...over,
});

describe('kiosk idle clock', () => {
  it('never times out on the welcome screen', () => {
    expect(idleState('welcome', 0, 10 * IDLE_RESET_MS)).toBe('active');
  });

  it('asks "still there?" after the warning time and resets after the reset time', () => {
    expect(idleState('menu', 1000, 1000 + IDLE_WARN_MS - 1)).toBe('active');
    expect(idleState('menu', 1000, 1000 + IDLE_WARN_MS)).toBe('warn');
    expect(idleState('review', 1000, 1000 + IDLE_RESET_MS - 1)).toBe('warn');
    expect(idleState('review', 1000, 1000 + IDLE_RESET_MS)).toBe('reset');
    expect(idleState('type', 0, IDLE_RESET_MS)).toBe('reset');
  });

  it('clears the order number screen on its own, shorter clock and never warns there', () => {
    expect(idleState('done', 0, DONE_RESET_MS - 1)).toBe('active');
    expect(idleState('done', 0, DONE_RESET_MS)).toBe('reset');
    expect(DONE_RESET_MS).toBeLessThan(IDLE_RESET_MS);
  });

  it('counts the seconds left, never below 0', () => {
    expect(secondsToReset(0, IDLE_WARN_MS)).toBe(15);
    expect(secondsToReset(0, IDLE_WARN_MS + 500)).toBe(15);
    expect(secondsToReset(0, IDLE_RESET_MS + 5000)).toBe(0);
  });
});

describe('kiosk staff exit', () => {
  it('opens only after enough quick taps', () => {
    let taps: number[] = [];
    for (let i = 0; i < EXIT_TAPS - 1; i++) {
      const r = registerExitTap(taps, i * 100);
      expect(r.open).toBe(false);
      taps = r.taps;
    }
    const last = registerExitTap(taps, (EXIT_TAPS - 1) * 100);
    expect(last.open).toBe(true);
    expect(last.taps).toEqual([]);
  });

  it('forgets slow taps, so a guest poking the logo now and then never opens it', () => {
    let taps: number[] = [];
    for (let i = 0; i < 20; i++) {
      const r = registerExitTap(taps, i * (EXIT_TAP_WINDOW_MS + 1));
      expect(r.open).toBe(false);
      taps = r.taps;
    }
  });
});

describe('kiosk order', () => {
  it('sends ids and quantities only, with the here/to-go choice and the key', () => {
    expect(toKioskRequest([line()], 'loc-1', 'key-1', 'DineIn')).toEqual({
      locationId: 'loc-1', idempotencyKey: 'key-1', orderType: 'DineIn',
      items: [{ menuItemId: 'item-1', quantity: 2, variantId: 'v-1', toppingIds: ['t-1'] }],
    });
  });

  it('never sends a price', () => {
    expect(JSON.stringify(toKioskRequest([line()], 'l', 'k', 'TakeAway'))).not.toContain('price');
  });

  it('shows the order number the same way the app and the TV do', () => {
    expect(orderNumber('3f2a9c10-aaaa-bbbb-cccc-0123456789ab')).toBe('6789AB');
    expect(orderNumber('3f2a9c10-aaaa-bbbb-cccc-0123456789ab')).toHaveLength(6);
  });

  it('reads the allergens of a dish', () => {
    expect(allergensOf({ nutrition: { allergens: ['milk', '', 'gluten'] } })).toEqual(['milk', 'gluten']);
    expect(allergensOf({ nutrition: null })).toEqual([]);
    expect(allergensOf({})).toEqual([]);
  });
});
