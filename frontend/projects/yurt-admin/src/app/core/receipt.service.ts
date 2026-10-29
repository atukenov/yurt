import { inject, Injectable } from '@angular/core';
import { BrandService } from 'shared-api';
import { DiningTable, FiscalReceipt, Order, OrderItem, SplitPaymentLine } from 'shared-models';
import { AdminLangService } from './lang.service';

/**
 * Prints a customer receipt for one order (Phase 6, `own_pos`). The receipt is built from the order the admin already
 * has, in a narrow 80 mm layout, and printed through the browser. A thermal printer can be picked in the print dialog;
 * direct printer support comes in its own step.
 */
@Injectable({ providedIn: 'root' })
export class ReceiptService {
  private lang = inject(AdminLangService);
  private brand = inject(BrandService);

  /**
   * `split` are the parts of a split payment; with two or more the receipt lists each one. `fiscal` is the order's sent
   * tax receipt: its QR code goes at the bottom, like on the network-printed receipt.
   */
  print(order: Order, split: SplitPaymentLine[] = [], fiscal?: FiscalReceipt | null): void {
    this.printHtml(this.html(order, split, fiscal));
  }

  /** Prints a kitchen ticket: the items to make (already filtered for a station), notes in bold, no prices. */
  printTicket(order: Order, items: OrderItem[], stationName?: string): void {
    this.printHtml(this.ticketHtml(order, items, stationName));
  }

  /** Prints the QR codes of tables to stick on them: one card per table with the cafe name, the code and the table name. */
  printTableQrCodes(tables: DiningTable[], branchName: string): void {
    const t = (k: string, p?: Record<string, string>) => this.lang.t(k, p);
    const esc = (s: string | null | undefined) =>
      (s ?? '').replace(
        /[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!,
      );
    const cards = tables
      .filter((x) => x.qrSvg)
      .map(
        (x) => `<div class="card"><p class="brand">${esc(this.brand.name())}</p>
          <img src="${x.qrSvg}" alt=""><p class="table">${esc(t('tables.tableN', { table: x.label }))}</p>
          <p class="hint">${esc(t('tables.scanToOrder'))}</p><p class="small">${esc(branchName)}</p></div>`,
      )
      .join('');
    this.printHtml(`<!doctype html><html><head><meta charset="utf-8"><title>${esc(t('tables.title'))}</title><style>
      @page { size: A4; margin: 10mm; }
      body { font-family: system-ui, sans-serif; margin: 0; color: #000; }
      .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 8mm; }
      .card { border: 1px dashed #999; border-radius: 4mm; padding: 6mm; text-align: center; break-inside: avoid; }
      .card img { width: 55mm; height: 55mm; }
      .brand { font-size: 16pt; font-weight: 700; margin: 0 0 3mm; }
      .table { font-size: 22pt; font-weight: 800; margin: 3mm 0 1mm; }
      .hint { font-size: 12pt; margin: 0; } .small { font-size: 9pt; color: #555; margin: 2mm 0 0; }
    </style></head><body><div class="grid">${cards}</div></body></html>`);
  }

  private printHtml(html: string): void {
    const frame = document.createElement('iframe');
    frame.setAttribute('aria-hidden', 'true');
    frame.style.cssText = 'position:fixed;width:0;height:0;border:0;right:0;bottom:0';
    document.body.appendChild(frame);
    const doc = frame.contentDocument;
    if (!doc) {
      frame.remove();
      return;
    }
    doc.open();
    doc.write(html);
    doc.close();
    const win = frame.contentWindow!;
    win.addEventListener('afterprint', () => frame.remove());
    // Give the iframe a moment to lay out before the print dialog opens.
    setTimeout(() => {
      win.focus();
      win.print();
    }, 100);
    // Fallback cleanup if the browser never fires afterprint.
    setTimeout(() => frame.remove(), 60_000);
  }

  html(order: Order, split: SplitPaymentLine[] = [], fiscal?: FiscalReceipt | null): string {
    const t = (k: string) => this.lang.t(k);
    const money = (n: number) =>
      `₸${Math.round(n)
        .toString()
        .replace(/\B(?=(\d{3})+(?!\d))/g, ' ')}`;
    const esc = (s: string | null | undefined) =>
      (s ?? '').replace(
        /[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!,
      );
    const when = new Date(order.completedAt ?? order.createdAt).toLocaleString(this.lang.lang());
    const points = order.loyaltyPointsSpent ?? 0;

    const lines = order.items
      .map((i) => {
        const extras = [i.variantLabel, ...i.toppings.map((x) => `+ ${x.toppingName}`)]
          .filter(Boolean)
          .map(esc)
          .join(', ');
        return `<tr><td>${i.quantity} × ${esc(i.menuItemName)}${extras ? `<br><small>${extras}</small>` : ''}</td><td class="r">${money(i.lineTotal)}</td></tr>`;
      })
      .join('');

    const row = (label: string, value: string, bold = false) =>
      `<tr${bold ? ' class="b"' : ''}><td>${esc(label)}</td><td class="r">${value}</td></tr>`;
    const totals = [
      order.discountAmount > 0 ? row(t('orders.subtotal'), money(order.subtotal)) : '',
      order.discountAmount > 0
        ? row(
            order.discountCode ? `🏷 ${order.discountCode}` : t('receipt.discount'),
            `−${money(order.discountAmount)}`,
          )
        : '',
      row(t('orders.total'), money(order.total), true),
      points > 0 ? row(t('orders.bonusPaid'), `−${money(points)}`) : '',
      points > 0 ? row(t('receipt.paid'), money(Math.max(0, order.total - points)), true) : '',
    ].join('');

    // Only a QR picture the server drew (an SVG data link) goes on paper; anything else is left out.
    const qr =
      fiscal?.status === 'Sent' && fiscal.ticketQr?.startsWith('data:image/svg+xml;base64,')
        ? fiscal.ticketQr
        : null;
    const fiscalBlock = qr
      ? `<hr><p class="c"><b>${esc(t('receipt.fiscal'))}</b>${fiscal!.checkNumber ? `<br>N ${esc(fiscal!.checkNumber)}` : ''}</p>
      <p class="c"><img class="qr" src="${esc(qr)}" alt=""><br>${esc(t('receipt.fiscalScan'))}</p>`
      : '';

    return `<!doctype html><html><head><meta charset="utf-8"><title>${esc(t('receipt.title'))}</title><style>
      @page { size: 80mm auto; margin: 4mm; }
      body { font: 12px/1.35 'Courier New', monospace; width: 72mm; margin: 0 auto; color: #000; }
      h1 { font-size: 15px; text-align: center; margin: 0 0 2px; }
      .c { text-align: center; } table { width: 100%; border-collapse: collapse; } td { padding: 2px 0; vertical-align: top; }
      .r { text-align: right; white-space: nowrap; padding-left: 6px; } .b td { font-weight: bold; font-size: 13px; }
      small { color: #333; } hr { border: 0; border-top: 1px dashed #000; margin: 6px 0; }
      .qr { width: 36mm; height: 36mm; image-rendering: pixelated; }
    </style></head><body>
      <h1>${esc(this.brand.name())}</h1>
      <p class="c">${esc(order.locationName)}<br>${esc(when)}<br>#${esc(order.id.slice(0, 8).toUpperCase())}${order.tableLabel ? `<br><b>${esc(this.lang.t('tables.tableN', { table: order.tableLabel }))}</b>` : ''}</p>
      <hr><table>${lines}</table><hr><table>${totals}</table><hr>
      ${split.length > 1 ? `<table>${split.map((p) => row(p.method, money(p.amount))).join('')}</table><hr>` : ''}
      <p class="c">${esc(split.length > 1 ? '' : order.paymentMethod ? t('receipt.method') + ': ' + order.paymentMethod : '')}<br>${esc(t('receipt.thanks'))}</p>
      ${fiscalBlock}
    </body></html>`;
  }

  ticketHtml(order: Order, items: OrderItem[], stationName?: string): string {
    const t = (k: string) => this.lang.t(k);
    const esc = (s: string | null | undefined) =>
      (s ?? '').replace(
        /[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!,
      );
    const when = new Date(order.createdAt).toLocaleTimeString(this.lang.lang(), {
      hour: '2-digit',
      minute: '2-digit',
    });
    const lines = items
      .map((i) => {
        const extras = [i.variantLabel, ...i.toppings.map((x) => `+ ${x.toppingName}`)]
          .filter(Boolean)
          .map(esc)
          .join(', ');
        return `<li><b>${i.quantity} × ${esc(i.menuItemName)}</b>${extras ? `<br><small>${extras}</small>` : ''}${i.notes ? `<br><strong>! ${esc(i.notes)}</strong>` : ''}</li>`;
      })
      .join('');

    return `<!doctype html><html><head><meta charset="utf-8"><title>${esc(t('kitchen.ticket'))}</title><style>
      @page { size: 80mm auto; margin: 4mm; }
      body { font: 14px/1.4 'Courier New', monospace; width: 72mm; margin: 0 auto; color: #000; }
      h1 { font-size: 22px; text-align: center; margin: 0; } p { text-align: center; margin: 2px 0; }
      ul { list-style: none; padding: 0; margin: 6px 0; } li { padding: 4px 0; border-bottom: 1px dashed #000; font-size: 15px; }
      small { color: #333; font-size: 13px; }
    </style></head><body>
      <h1>#${esc(order.id.slice(0, 8).toUpperCase())}</h1>
      ${order.tableLabel ? `<h1>${esc(this.lang.t('tables.tableN', { table: order.tableLabel }))}</h1>` : ''}
      <p>${esc(stationName ?? '')}${stationName ? ' · ' : ''}${esc(when)}</p>
      <p>${esc(order.customerName)}</p>
      <ul>${lines}</ul>
    </body></html>`;
  }
}
