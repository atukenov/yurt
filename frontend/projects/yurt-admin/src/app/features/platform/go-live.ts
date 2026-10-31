import { GoLiveChecklist } from 'shared-models';

/** Guides live in the repository's docs folder; GitHub shows them readable, with working section links. */
const DOCS = 'https://github.com/atukenov/yurt/blob/main/docs/';

/** The guide for each checklist line. */
const GUIDES: Record<string, string> = {
  master_key: 'live-server-setup.md#1-master-key-locks-the-cafes-secret-keys',
  payment_return_url: 'live-server-setup.md#2-return-link-after-online-payment',
  telegram_alerts: 'monitoring.md#1-error-alerts-in-telegram-about-5-minutes',
  billing_keys: 'billing.md#setup-test-mode',
  domain: 'go-live.md#domain',
  brand: 'go-live.md#brand',
  menu: 'go-live.md#menu',
  branches: 'go-live.md#branches',
  stripe: 'stripe-payments.md#setting-up-a-cafe-about-10-minutes',
  firebase: 'push-notifications-setup.md',
  whatsapp: 'go-live.md#whatsapp-codes-green-api',
  fiscal: 'go-live.md#tax-receipts',
  billing: 'billing.md#how-it-works',
};

export function goLiveGuide(key: string): string {
  return DOCS + (GUIDES[key] ?? 'go-live.md');
}

/** Names of settings and secrets (for example Stripe:SecretKey) are shown as they are, not translated. */
export function isCodeName(part: string): boolean {
  return part.includes(':');
}

/** How many lines are done out of those that are needed (lines that are off do not count). */
export function goLiveProgress(c: GoLiveChecklist | null): { done: number; needed: number } {
  const items = c ? [...c.server, ...c.cafe].filter((i) => i.state !== 'off') : [];
  return { done: items.filter((i) => i.state === 'done').length, needed: items.length };
}
