import { CreateKioskOrderRequest, OrderType } from 'shared-models';
import { CartLine } from './cart';

/** Screens of the self-order kiosk, in the order a guest sees them. */
export type KioskStep = 'welcome' | 'type' | 'menu' | 'review' | 'done';

/** After this long without a touch, the screen asks "Still there?". */
export const IDLE_WARN_MS = 45_000;
/** After this long without a touch, the order is thrown away and the welcome screen comes back. */
export const IDLE_RESET_MS = 60_000;
/** The order number stays on screen this long, so the next guest does not see it for long. */
export const DONE_RESET_MS = 25_000;

/** What the idle clock says: nothing to do, ask "still there?", or start over. */
export type IdleState = 'active' | 'warn' | 'reset';

/**
 * Whether a guest has walked away. The welcome screen never times out (nobody has started), the number screen has its
 * own, shorter clock, and every other screen asks first and then resets.
 */
export function idleState(step: KioskStep, lastTouch: number, now: number): IdleState {
  if (step === 'welcome') return 'active';
  const idle = now - lastTouch;
  if (step === 'done') return idle >= DONE_RESET_MS ? 'reset' : 'active';
  if (idle >= IDLE_RESET_MS) return 'reset';
  return idle >= IDLE_WARN_MS ? 'warn' : 'active';
}

/** Whole seconds left before the reset, for the "Still there?" countdown. */
export function secondsToReset(lastTouch: number, now: number): number {
  return Math.max(0, Math.ceil((IDLE_RESET_MS - (now - lastTouch)) / 1000));
}

/** Taps on the logo needed to open the staff exit, and the time they must all fall in. */
export const EXIT_TAPS = 5;
export const EXIT_TAP_WINDOW_MS = 3_000;

/** Adds a tap to the recent ones and says whether the secret staff exit is now open. Old taps drop out. */
export function registerExitTap(taps: number[], now: number): { taps: number[]; open: boolean } {
  const recent = [...taps.filter((t) => now - t <= EXIT_TAP_WINDOW_MS), now];
  return recent.length >= EXIT_TAPS ? { taps: [], open: true } : { taps: recent, open: false };
}

/** The order as the server wants it. Prices are never sent: the server takes them from the menu. */
export function toKioskRequest(
  lines: CartLine[], locationId: string, idempotencyKey: string, orderType: OrderType,
): CreateKioskOrderRequest {
  return {
    locationId,
    idempotencyKey,
    orderType,
    items: lines.map((l) => ({
      menuItemId: l.menuItemId,
      quantity: l.quantity,
      variantId: l.variantId,
      toppingIds: l.toppings.map((t) => t.id),
    })),
  };
}

/** The number the guest carries to the counter and hears called: the last 6 characters of the order id, like the app and the TV screen. */
export function orderNumber(orderId: string): string {
  return orderId.slice(-6).toUpperCase();
}

/** The allergens of a dish, as codes, never empty strings. */
export function allergensOf(item: { nutrition?: { allergens?: string[] | null } | null }): string[] {
  return (item.nutrition?.allergens ?? []).filter((a) => !!a);
}
