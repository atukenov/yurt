import test from 'node:test';
import assert from 'node:assert/strict';
import { buildCheck, redact } from './webkassa-check.mjs';

test('check body matches the adapter: VAT inside the price, same payment types merged', () => {
  const body = buildCheck('t', {
    cashbox: 'SWK1', kind: 'return', externalId: 'x', vat: 16,
    lines: [{ name: 'Tea', count: 1, price: 2000, discount: 100 }],
    payments: [{ type: 1, sum: 1000 }, { type: 1, sum: 900 }],
  });
  assert.equal(body.OperationType, 3);
  assert.equal(body.Positions[0].Tax, 262.07); // 1900 * 16 / 116, as in WebkassaFiscalRegistrarTests
  assert.deepEqual(body.Payments, [{ Sum: 1900, PaymentType: 1 }]);
});

test('report hides passwords, keys and tokens', () => {
  const out = redact({ Login: 'me', Password: 'secret', Data: { Token: 'abc' }, List: [{ apiKey: 'k' }] });
  assert.equal(out.Login, 'me');
  assert.doesNotMatch(JSON.stringify(out), /secret|abc|"k"/);
});
