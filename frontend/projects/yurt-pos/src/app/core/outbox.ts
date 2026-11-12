import { CreateCounterSaleRequest } from 'shared-models';

/**
 * A sale saved on the tablet because it could not reach the server yet. It keeps its own key, so however often it is
 * sent (a lost answer, a reload, two tabs), the server makes it once.
 */
export interface QueuedSale {
  request: CreateCounterSaleRequest & { soldAt: string; handedOver: true };
  total: number;
  /** "waiting" = will be sent; "refused" = the server said no (for example a deleted item), a person must look. */
  state: 'waiting' | 'refused';
  attempts: number;
  /** The server's reason when refused. */
  error?: string | null;
}

/** What to do with a queued sale after one try. */
export type SendOutcome = 'sent' | 'retry' | 'refused' | 'signin';

/**
 * Sorts an HTTP answer: 2xx sent; 401/403 needs someone to sign in again (keep the sale); 400/404/409/422 the server
 * refused this sale (keep it for a person); no answer, 408, 429 or 5xx try again later.
 */
export function outcomeOf(status: number): SendOutcome {
  if (status >= 200 && status < 300) return 'sent';
  if (status === 401 || status === 403) return 'signin';
  if (status === 400 || status === 404 || status === 409 || status === 422) return 'refused';
  return 'retry';
}

/**
 * A sale that could not be sent becomes a queued one. It is marked as handed over: by the time it reaches the server
 * the guest has it, so it does not show up on the kitchen screen late. Its time is when it was made.
 */
export function toQueued(request: CreateCounterSaleRequest, total: number, soldAt: Date): QueuedSale {
  return {
    request: { ...request, soldAt: soldAt.toISOString(), handedOver: true, etaMinutes: null },
    total,
    state: 'waiting',
    attempts: 0,
  };
}

/** Adds a sale unless one with the same key is already there. */
export function enqueue(queue: QueuedSale[], sale: QueuedSale): QueuedSale[] {
  return queue.some((q) => q.request.idempotencyKey === sale.request.idempotencyKey) ? queue : [...queue, sale];
}

/** Applies the result of one try to the queue. */
export function applyOutcome(queue: QueuedSale[], key: string, outcome: SendOutcome, error?: string | null): QueuedSale[] {
  if (outcome === 'sent') return queue.filter((q) => q.request.idempotencyKey !== key);
  return queue.map((q) => {
    if (q.request.idempotencyKey !== key) return q;
    const attempts = q.attempts + 1;
    return outcome === 'refused' ? { ...q, attempts, state: 'refused', error: error ?? null } : { ...q, attempts };
  });
}

/** The sales to send now, oldest first. Refused ones wait for a person. */
export function due(queue: QueuedSale[]): QueuedSale[] {
  return queue
    .filter((q) => q.state === 'waiting')
    .sort((a, b) => a.request.soldAt.localeCompare(b.request.soldAt));
}

/** Sends a refused sale again (after the cause was fixed, for example the item is back on the menu). */
export function retryRefused(queue: QueuedSale[], key: string): QueuedSale[] {
  return queue.map((q) => (q.request.idempotencyKey === key ? { ...q, state: 'waiting', error: null } : q));
}

/** A queued sale older than the server takes (7 days) can never be sent; it is shown so a person enters it by hand. */
export function isTooOld(sale: QueuedSale, now: Date, maxDays = 7): boolean {
  return now.getTime() - new Date(sale.request.soldAt).getTime() > maxDays * 24 * 3600 * 1000;
}
