#!/usr/bin/env node
// Webkassa check (Phase 6, own_pos). Runs the calls Yurt's Webkassa adapter makes against a Webkassa TEST account and
// saves every question and answer to webkassa-check-report.json, so the "unverified" guesses in
// backend/src/Yurt.Infrastructure/Fiscal/WebkassaFiscalRegistrar.cs can be checked and fixed. Needs Node.js 18+.
//
//   WEBKASSA_LOGIN=... WEBKASSA_PASSWORD=... node scripts/webkassa-check.mjs
//
// Optional: WEBKASSA_API_KEY (partner key), WEBKASSA_CASHBOX (register number; otherwise the first one Webkassa lists),
// WEBKASSA_VAT (for example 16; empty = no VAT), WEBKASSA_URL (default the test server).
// The report hides the password, the key and login tokens. It sends real receipts, so it refuses the live server.
import { randomUUID } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export const TEST_URL = 'https://devkkm.webkassa.kz';

// Same codes as WebkassaFiscalRegistrar (all unverified).
const OperationSale = 2;
const OperationSaleReturn = 3;
const PaymentCash = 0;
const PaymentCard = 1;
const PaymentMobile = 4;
const TaxNone = 0;
const TaxVat = 100;
const UnitPiece = 796;

/** The check body, built the same way as WebkassaFiscalRegistrar.BuildCheck. */
export function buildCheck(token, { cashbox, kind, externalId, lines, payments, vat }) {
  const positions = lines.map((l) => {
    const lineSum = l.price * l.count - l.discount;
    const tax = vat > 0 ? Math.round((lineSum * vat * 100) / (100 + vat)) / 100 : 0;
    return {
      Count: l.count, Price: l.price, Discount: l.discount, Markup: 0,
      TaxType: vat > 0 ? TaxVat : TaxNone, TaxPercent: vat || 0, Tax: tax,
      PositionName: l.name, UnitCode: UnitPiece, IsStorno: false,
    };
  });
  const byType = new Map();
  for (const p of payments) byType.set(p.type, (byType.get(p.type) ?? 0) + p.sum);
  return {
    Token: token,
    CashboxUniqueNumber: cashbox,
    OperationType: kind === 'sale' ? OperationSale : OperationSaleReturn,
    Positions: positions,
    Payments: [...byType].map(([type, sum]) => ({ Sum: sum, PaymentType: type })),
    Change: 0,
    RoundType: 2,
    ExternalCheckNumber: externalId,
  };
}

/** Copy of a body or answer with secrets replaced, safe to share. */
export function redact(value) {
  if (Array.isArray(value)) return value.map(redact);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.entries(value).map(([k, v]) =>
      [k, /^(password|token|apikey|x-api-key)$/i.test(k) && typeof v === 'string' ? `<hidden, ${v.length} chars>` : redact(v)]));
  }
  return value;
}

async function main() {
  const env = process.env;
  const base = (env.WEBKASSA_URL || TEST_URL).replace(/\/+$/, '');
  if (!env.WEBKASSA_LOGIN || !env.WEBKASSA_PASSWORD) {
    console.error('Set WEBKASSA_LOGIN and WEBKASSA_PASSWORD (the test account login), then run again.');
    process.exit(2);
  }
  if (/\/\/kkm\.webkassa\.kz/i.test(base)) {
    console.error('This sends test receipts; it will not run against the live server kkm.webkassa.kz.');
    process.exit(2);
  }
  const vat = Number(env.WEBKASSA_VAT || 0);
  const steps = [];
  const report = { ranAt: new Date().toISOString(), server: base, vat, steps };
  const save = () => writeFileSync('webkassa-check-report.json', JSON.stringify(report, null, 2));

  async function call(name, path, body, { apiKey = env.WEBKASSA_API_KEY } = {}) {
    const headers = { 'Content-Type': 'application/json' };
    if (apiKey) headers['X-API-KEY'] = apiKey;
    const step = { name, path, sent: redact(body), sentApiKeyHeader: Boolean(apiKey) };
    try {
      const res = await fetch(`${base}/${path}`, { method: 'POST', headers, body: JSON.stringify(body), signal: AbortSignal.timeout(30000) });
      const text = await res.text();
      let json = null;
      try { json = JSON.parse(text); } catch { step.rawAnswer = text.slice(0, 2000); }
      Object.assign(step, { status: res.status, answer: redact(json) });
      steps.push(step);
      console.log(`${res.ok && !json?.Errors?.length ? '✓' : '✗'} ${name}: HTTP ${res.status}${json?.Errors?.length ? ' ' + JSON.stringify(json.Errors) : ''}`);
      save();
      return json;
    } catch (e) {
      Object.assign(step, { failed: String(e) });
      steps.push(step);
      console.log(`✗ ${name}: ${e}`);
      save();
      return null;
    }
  }

  const login = await call('1. Log in', 'api/Authorize', { Login: env.WEBKASSA_LOGIN, Password: env.WEBKASSA_PASSWORD });
  const token = login?.Data?.Token;
  if (!token) {
    console.log('\nNo login token came back, so the check stops here. Send webkassa-check-report.json to Claude.');
    return;
  }
  if (env.WEBKASSA_API_KEY) await call('1b. Log in without the partner key', 'api/Authorize',
    { Login: env.WEBKASSA_LOGIN, Password: env.WEBKASSA_PASSWORD }, { apiKey: '' });

  const boxes = await call('2. List cash registers', 'api/Cashboxes', { Token: token });
  const listed = boxes?.Data?.List ?? boxes?.Data ?? [];
  const cashbox = env.WEBKASSA_CASHBOX || (Array.isArray(listed) ? listed[0]?.UniqueNumber : undefined);
  report.cashbox = cashbox ?? null;
  if (!cashbox) {
    console.log('\nNo register number found. Set WEBKASSA_CASHBOX and run again, or send the report to Claude.');
    save();
    return;
  }

  const cashSale = { cashbox, kind: 'sale', externalId: randomUUID(), vat,
    lines: [{ name: 'Yurt test: cappuccino', count: 1, price: 1200, discount: 0 }],
    payments: [{ type: PaymentCash, sum: 1200 }] };
  await call('3. Sale paid in cash', 'api/Check', buildCheck(token, cashSale));
  await call('4. Same sale again (same ExternalCheckNumber, should not make a second receipt)', 'api/Check', buildCheck(token, cashSale));

  const splitSale = { cashbox, kind: 'sale', externalId: randomUUID(), vat,
    lines: [
      { name: 'Yurt test: latte', count: 2, price: 1400, discount: 300 },
      { name: 'Yurt test: croissant', count: 1, price: 900, discount: 0 },
    ],
    payments: [{ type: PaymentCard, sum: 2000 }, { type: PaymentMobile, sum: 1400 }] };
  await call('5. Sale with a discount, paid by card and Kaspi', 'api/Check', buildCheck(token, splitSale));
  await call('6. Return of that sale', 'api/Check', buildCheck(token, { ...splitSale, kind: 'return', externalId: randomUUID() }));

  await call('7. Call with a wrong token (which error code means "log in again"?)', 'api/Check',
    buildCheck('not-a-real-token', { ...cashSale, externalId: randomUUID() }));
  await call('8. Close the shift (Z report)', 'api/ZReport', { Token: token, CashboxUniqueNumber: cashbox });

  save();
  console.log('\nDone. Send webkassa-check-report.json to Claude (passwords and tokens are hidden in it).');
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? '').href) await main();
