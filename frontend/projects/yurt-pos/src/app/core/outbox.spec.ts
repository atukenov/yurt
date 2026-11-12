import { PaymentMethod } from 'shared-models';
import { applyOutcome, due, enqueue, isTooOld, outcomeOf, retryRefused, toQueued } from './outbox';

const req = (key: string) => ({
  locationId: 'b', paymentMethod: PaymentMethod.Cash, idempotencyKey: key, handedOver: false, etaMinutes: 5,
  items: [{ menuItemId: 'x', quantity: 1 }],
});

describe('offline outbox', () => {
  it('keeps the sale time, marks it handed over and drops the kitchen time', () => {
    const at = new Date('2026-11-12T08:00:00Z');
    const q = toQueued(req('a'), 500, at);
    expect(q.request.soldAt).toBe('2026-11-12T08:00:00.000Z');
    expect(q.request.handedOver).toBe(true);
    expect(q.request.etaMinutes).toBeNull();
    expect(q.state).toBe('waiting');
  });

  it('never queues the same sale twice', () => {
    const at = new Date();
    let queue = enqueue([], toQueued(req('a'), 1, at));
    queue = enqueue(queue, toQueued(req('a'), 1, at));
    expect(queue.length).toBe(1);
  });

  it('sorts server answers', () => {
    expect(outcomeOf(201)).toBe('sent');
    expect(outcomeOf(200)).toBe('sent'); // the server already had it
    expect(outcomeOf(0)).toBe('retry'); // no internet
    expect(outcomeOf(503)).toBe('retry');
    expect(outcomeOf(429)).toBe('retry');
    expect(outcomeOf(401)).toBe('signin');
    expect(outcomeOf(400)).toBe('refused');
    expect(outcomeOf(422)).toBe('refused');
  });

  it('removes sent sales, keeps the rest, and parks refused ones until a person retries', () => {
    let queue = [
      toQueued(req('b'), 1, new Date('2026-11-12T09:00:00Z')),
      toQueued(req('a'), 1, new Date('2026-11-12T08:00:00Z')),
      toQueued(req('c'), 1, new Date('2026-11-12T10:00:00Z')),
    ];
    expect(due(queue).map((q) => q.request.idempotencyKey)).toEqual(['a', 'b', 'c']);
    queue = applyOutcome(queue, 'a', 'sent');
    queue = applyOutcome(queue, 'b', 'refused', 'One of the items is no longer on the menu.');
    queue = applyOutcome(queue, 'c', 'retry');
    expect(queue.map((q) => [q.request.idempotencyKey, q.state, q.attempts])).toEqual([['b', 'refused', 1], ['c', 'waiting', 1]]);
    expect(due(queue).map((q) => q.request.idempotencyKey)).toEqual(['c']);
    queue = retryRefused(queue, 'b');
    expect(due(queue).map((q) => q.request.idempotencyKey)).toEqual(['b', 'c']);
  });

  it('knows when a sale is too old for the server', () => {
    const q = toQueued(req('a'), 1, new Date('2026-11-01T00:00:00Z'));
    expect(isTooOld(q, new Date('2026-11-05T00:00:00Z'))).toBe(false);
    expect(isTooOld(q, new Date('2026-11-09T00:00:00Z'))).toBe(true);
  });
});
