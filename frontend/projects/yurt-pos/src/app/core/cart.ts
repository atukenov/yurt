import { CreateCounterSaleRequest, MenuItem, MenuTopping, PaymentMethod } from 'shared-models';

/** One line on the till: an item in one size with its add-ons. Prices are only for the screen; the server sets the real ones. */
export interface CartLine {
  key: string;
  menuItemId: string;
  name: string;
  variantId: string | null;
  variantLabel: string | null;
  toppings: { id: string; name: string; price: number }[];
  unitPrice: number;
  quantity: number;
}

/** The methods a cashier can take at the counter, in the order the buttons show. */
export const COUNTER_METHODS: PaymentMethod[] = [
  PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.KaspiBank, PaymentMethod.HalykBank, PaymentMethod.FreedomBank,
];

/** The counter methods a branch takes. A branch with no list takes them all. */
export function branchMethods(offered: string[] | null | undefined): PaymentMethod[] {
  if (!offered || offered.length === 0) return COUNTER_METHODS;
  return COUNTER_METHODS.filter((m) => offered.includes(m));
}

/** Lines with the same item, size and add-ons are the same line. */
export function lineKey(menuItemId: string, variantId: string | null, toppingIds: string[]): string {
  return [menuItemId, variantId ?? '', ...[...toppingIds].sort()].join('|');
}

/** The size picked when the cashier taps an item without choosing: the default one, else the first. */
export function defaultVariant(item: MenuItem) {
  const variants = item.variants ?? [];
  return variants.find((v) => v.isDefault) ?? variants[0] ?? null;
}

/** Whether tapping the item needs a choice first (sizes or add-ons). */
export function needsOptions(item: MenuItem): boolean {
  return (item.variants?.length ?? 0) > 1 || (item.availableToppings?.some((t) => t.isAvailable) ?? false);
}

/**
 * Picks an add-on. Add-ons with the same group (milk, syrup...) replace each other; add-ons with no group are plain
 * on/off ticks.
 */
export function toggleTopping(chosen: MenuTopping[], topping: MenuTopping): MenuTopping[] {
  if (chosen.some((t) => t.id === topping.id)) return chosen.filter((t) => t.id !== topping.id);
  const rest = topping.group ? chosen.filter((t) => t.group !== topping.group) : chosen;
  return [...rest, topping];
}

export function addLine(
  lines: CartLine[], item: MenuItem, variantId: string | null, toppings: MenuTopping[], quantity = 1,
): CartLine[] {
  const variant = item.variants?.find((v) => v.id === variantId) ?? null;
  const key = lineKey(item.id, variant?.id ?? null, toppings.map((t) => t.id));
  const existing = lines.find((l) => l.key === key);
  if (existing) return lines.map((l) => (l.key === key ? { ...l, quantity: l.quantity + quantity } : l));
  return [...lines, {
    key,
    menuItemId: item.id,
    name: item.name,
    variantId: variant?.id ?? null,
    variantLabel: variant?.label ?? null,
    toppings: toppings.map((t) => ({ id: t.id, name: t.name, price: t.price })),
    unitPrice: (variant?.price ?? item.price) + toppings.reduce((s, t) => s + t.price, 0),
    quantity,
  }];
}

/** Changes a line's quantity by `delta`; a line that drops to 0 goes away. */
export function changeQuantity(lines: CartLine[], key: string, delta: number): CartLine[] {
  return lines
    .map((l) => (l.key === key ? { ...l, quantity: Math.min(999, l.quantity + delta) } : l))
    .filter((l) => l.quantity > 0);
}

export function cartTotal(lines: CartLine[]): number {
  return lines.reduce((s, l) => s + l.unitPrice * l.quantity, 0);
}

export function cartCount(lines: CartLine[]): number {
  return lines.reduce((s, l) => s + l.quantity, 0);
}

/** Change to give back for cash. Null while the guest has not handed over enough. */
export function changeDue(total: number, given: number | null): number | null {
  if (given === null || Number.isNaN(given) || given < total) return null;
  return Math.round((given - total) * 100) / 100;
}

export function toRequest(
  lines: CartLine[], locationId: string, method: PaymentMethod, idempotencyKey: string,
  handedOver: boolean, etaMinutes: number | null,
): CreateCounterSaleRequest {
  return {
    locationId,
    paymentMethod: method,
    idempotencyKey,
    handedOver,
    etaMinutes: handedOver ? null : etaMinutes,
    items: lines.map((l) => ({
      menuItemId: l.menuItemId,
      quantity: l.quantity,
      variantId: l.variantId,
      toppingIds: l.toppings.map((t) => t.id),
    })),
  };
}

/** The cart's lines as the printed receipt shows them: size and add-ons after the name, like the server's receipt. */
export function receiptLines(lines: CartLine[]): { quantity: number; name: string; extras: string; total: number }[] {
  return lines.map((l) => ({
    quantity: l.quantity,
    name: l.name,
    extras: [l.variantLabel, ...l.toppings.map((t) => `+ ${t.name}`)].filter((s) => !!s && s.trim()).join(', '),
    total: l.unitPrice * l.quantity,
  }));
}

/** The number on a receipt printed before the server had the sale: "T-" and the start of the sale's key. */
export function offlineNumber(key: string): string {
  return 'T-' + key.replace(/-/g, '').slice(0, 6).toUpperCase();
}
