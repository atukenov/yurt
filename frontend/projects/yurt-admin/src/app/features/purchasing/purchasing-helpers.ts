import { ToOrderLine } from 'shared-models';

/**
 * Link that opens WhatsApp with the order text ready to send. Kazakh numbers typed as 8 7xx… become +7 7xx….
 * Without a number, WhatsApp asks who to send it to.
 */
export function whatsappLink(phone: string | null | undefined, text: string): string {
  let digits = (phone ?? '').replace(/\D/g, '');
  if (digits.length === 11 && digits.startsWith('8')) digits = '7' + digits.slice(1);
  return `https://wa.me/${digits}?text=${encodeURIComponent(text)}`;
}

/** A plain number for messages: up to 3 decimals, no trailing zeros. */
export function plain(n: number): string {
  return String(Math.round(n * 1000) / 1000);
}

/** One line of an order message, e.g. "Oat milk — 2 × 6 x 1 L (12000 ml)" or "Lids — 90 pcs". */
export function orderLineText(line: { name: string; unit: string; quantity: number; packs?: number | null; packName?: string | null }): string {
  const amount = `${plain(line.quantity)} ${line.unit}`;
  return line.packs && line.packName
    ? `${line.name} — ${plain(line.packs)} × ${line.packName} (${amount})`
    : `${line.name} — ${amount}`;
}

/** The whole order message: greeting line, the lines, and who it is for. */
export function orderText(
  heading: string,
  lines: { name: string; unit: string; quantity: number; packs?: number | null; packName?: string | null }[],
  footer: string,
): string {
  return [heading, '', ...lines.map((l) => '• ' + orderLineText(l)), '', footer].join('\n');
}

/**
 * Packs for an amount the person changed on the "what to order" list: whole packs when the pack size is known,
 * otherwise null. The estimated cost follows the packs at the usual pack price.
 */
export function packsFor(line: ToOrderLine, quantity: number): { packs: number | null; cost: number | null } {
  if (!line.packSize || line.packSize <= 0 || !(quantity > 0)) return { packs: null, cost: null };
  const packs = Math.ceil(quantity / line.packSize - 1e-9);
  const packPrice = line.packs && line.estimatedCost != null ? line.estimatedCost / line.packs : null;
  return { packs, cost: packPrice === null ? null : Math.round(packs * packPrice * 100) / 100 };
}

/** Sum of the known line totals. */
export function invoiceTotal(lines: { total: number | null | undefined }[]): number {
  return lines.reduce((sum, l) => sum + (typeof l.total === 'number' && !Number.isNaN(l.total) ? l.total : 0), 0);
}

/** yyyy-mm-dd of a date in the person's own time zone (for <input type="date">). */
export function isoDay(date: Date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}
