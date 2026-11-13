import { PaymentMethod } from 'shared-models';
import { CartLine, receiptLines } from './cart';

/** What the guest-facing screen shows. The till makes it; the second screen only draws it. */
export interface GuestView {
  /** idle = nothing being rung up (shows the cafe and promotions); cart = items being added; pay = payment step; done = sale finished. */
  phase: 'idle' | 'cart' | 'pay' | 'done';
  lang: 'en' | 'ru' | 'kk';
  lines: { quantity: number; name: string; extras: string; total: number; weightGrams?: number }[];
  total: number;
  discount: number;
  /** What the guest pays for the bill (total minus discount). */
  payable: number;
  tip: number;
  method: PaymentMethod | null;
  /** Cash handed over so far, when paying cash. */
  cashGiven: number | null;
  /** Change to give back, when known. */
  change: number | null;
  orderType: 'DineIn' | 'TakeAway';
  /** What the finished sale came to (done phase). */
  paid: number;
  /** The number on the finished sale (done phase). */
  saleNo: string | null;
  /** True when the finished sale was saved on the till without internet. */
  saved: boolean;
  /** When the till wrote this, ISO time. */
  at: string;
}

export const IDLE_VIEW: Omit<GuestView, 'lang' | 'at'> = {
  phase: 'idle', lines: [], total: 0, discount: 0, payable: 0, tip: 0, method: null, cashGiven: null, change: null,
  orderType: 'TakeAway', paid: 0, saleNo: null, saved: false,
};

export interface GuestViewInput {
  step: 'cart' | 'pay' | 'done';
  lang: GuestView['lang'];
  lines: CartLine[];
  total: number;
  discount: number;
  payable: number;
  tip: number;
  method: PaymentMethod | null;
  cashGiven: number | null;
  change: number | null;
  orderType: 'DineIn' | 'TakeAway';
  /** Set on the done step: what the sale came to. */
  paid: number;
  /** Set on the done step: the finished sale's number, and whether it was only saved on the till. */
  saleNo: string | null;
  saved: boolean;
}

/** Builds what the guest sees from the till's state. An empty cart before payment is the idle screen. */
export function guestView(input: GuestViewInput, now: Date = new Date()): GuestView {
  const at = now.toISOString();
  if (input.step === 'done') {
    return {
      ...IDLE_VIEW, phase: 'done', lang: input.lang, method: input.method, orderType: input.orderType, paid: input.paid,
      saleNo: input.saleNo, saved: input.saved, at,
    };
  }
  if (input.lines.length === 0) return { ...IDLE_VIEW, lang: input.lang, at };
  const paying = input.step === 'pay';
  return {
    phase: paying ? 'pay' : 'cart',
    lang: input.lang,
    lines: receiptLines(input.lines),
    total: input.total,
    discount: input.discount,
    payable: input.payable,
    // Tip, method and cash only matter once the cashier is on the payment step.
    tip: paying ? input.tip : 0,
    method: paying ? input.method : null,
    cashGiven: paying ? input.cashGiven : null,
    change: paying ? input.change : null,
    orderType: input.orderType,
    paid: 0,
    saleNo: null,
    saved: false,
    at,
  };
}

/** A screen nobody has updated for this long (the till was closed) goes back to the idle screen. */
export const STALE_MS = 60 * 60 * 1000;

export function isStale(view: GuestView, now: Date = new Date()): boolean {
  const t = Date.parse(view.at);
  return Number.isNaN(t) || now.getTime() - t > STALE_MS;
}

/** Reads a saved view; anything unreadable or stale is the idle screen. */
export function parseView(raw: string | null, fallbackLang: GuestView['lang'], now: Date = new Date()): GuestView {
  const idle: GuestView = { ...IDLE_VIEW, lang: fallbackLang, at: now.toISOString() };
  if (!raw) return idle;
  try {
    const v = JSON.parse(raw) as GuestView;
    if (!v || !['idle', 'cart', 'pay', 'done'].includes(v.phase) || !Array.isArray(v.lines)) return idle;
    return isStale(v, now) ? idle : v;
  } catch {
    return idle;
  }
}

/** The line the guest is told when paying: what to do with the card / cash / bank app. */
export function payPromptKey(method: PaymentMethod | null): string {
  switch (method) {
    case PaymentMethod.Cash: return 'guest.payCash';
    case PaymentMethod.Card: return 'guest.payCard';
    case null: return 'guest.payWait';
    default: return 'guest.payQr';
  }
}
