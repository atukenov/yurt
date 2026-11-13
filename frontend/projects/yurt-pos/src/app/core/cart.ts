import { CreateCounterSaleRequest, MenuItem, MenuTopping, OptionRule, OrderType, PaymentMethod } from 'shared-models';

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

/** The rule for an option group. A group nobody set up means: at most one, may be none. */
export function ruleOf(rules: OptionRule[] | undefined, group: string): OptionRule {
  return rules?.find((r) => r.group === group) ?? { group, title: group, required: false, min: 0, max: 1 };
}

/**
 * Picks an add-on. A group allowing one choice (milk, syrup...) swaps the old choice for the new one; a group
 * allowing several stops at its largest number; add-ons with no group are plain on/off ticks. A required group
 * keeps its last choice until another one is picked.
 */
export function toggleTopping(chosen: MenuTopping[], topping: MenuTopping, rules?: OptionRule[]): MenuTopping[] {
  const group = topping.group;
  const rule = group ? ruleOf(rules, group) : null;
  const inGroup = group ? chosen.filter((t) => t.group === group).length : 0;
  if (chosen.some((t) => t.id === topping.id)) {
    if (rule && rule.min > 0 && inGroup <= rule.min) return chosen;
    return chosen.filter((t) => t.id !== topping.id);
  }
  if (!group || !rule) return [...chosen, topping];
  if (rule.max === 1) return [...chosen.filter((t) => t.group !== group), topping];
  if (rule.max !== null && inGroup >= rule.max) return chosen;
  return [...chosen, topping];
}

/** The first option group the cashier still has to choose from, or null when every rule is met. */
export function missingChoice(item: MenuItem, chosen: MenuTopping[]): OptionRule | null {
  const offered = (item.availableToppings ?? []).filter((t) => t.isAvailable);
  for (const group of new Set(offered.filter((t) => t.group).map((t) => t.group!))) {
    const rule = ruleOf(item.optionRules, group);
    const need = Math.min(rule.min, offered.filter((t) => t.group === group).length);
    if (chosen.filter((t) => t.group === group).length < need) return rule;
  }
  return null;
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

/** A discount a manager approved at the till. The server checks the PIN again when the sale arrives. */
export interface TillDiscount {
  kind: 'percent' | 'amount';
  value: number;
  pin: string;
  reason: string;
}

/** How much a discount takes off the bill: a percent of it, or a fixed amount, never more than the bill. */
export function discountOf(total: number, discount: Pick<TillDiscount, 'kind' | 'value'> | null): number {
  if (!discount || !(discount.value > 0)) return 0;
  const off = discount.kind === 'percent' ? (total * Math.min(discount.value, 100)) / 100 : discount.value;
  return Math.round(Math.min(off, total) * 100) / 100;
}

/** What the cashier chose for a tip: nothing, a percent of the bill, or a typed amount. */
export type TipChoice = { kind: 'none' } | { kind: 'percent'; value: number } | { kind: 'amount'; value: number };

/** The tip buttons on the payment step. */
export const TIP_PERCENTS = [5, 10, 15];

/** How much the tip is: a percent of what the guest pays for the bill, or the typed amount. Never negative. */
export function tipOf(bill: number, choice: TipChoice): number {
  if (choice.kind === 'none' || !(choice.value > 0) || !(bill >= 0)) return 0;
  const tip = choice.kind === 'percent' ? (bill * Math.min(choice.value, 100)) / 100 : choice.value;
  return Math.round(Math.min(tip, 1_000_000) * 100) / 100;
}

export function toRequest(
  lines: CartLine[], locationId: string, method: PaymentMethod, idempotencyKey: string,
  handedOver: boolean, etaMinutes: number | null, orderType: OrderType = 'TakeAway',
  discount: TillDiscount | null = null,
  tip = 0,
  tipMethod: PaymentMethod | null = null,
): CreateCounterSaleRequest {
  return {
    ...(tip > 0 ? { tip, ...(tipMethod && tipMethod !== method ? { tipMethod } : {}) } : {}),
    ...(discount ? {
      discount: {
        percent: discount.kind === 'percent' ? discount.value : null,
        amount: discount.kind === 'amount' ? discount.value : null,
        pin: discount.pin,
        reason: discount.reason,
      },
    } : {}),
    orderType,
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
