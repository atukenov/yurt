import { GoLiveChecklist, GoLiveItem } from 'shared-models';
import { goLiveGuide, goLiveProgress, isCodeName } from './go-live';

const item = (key: string, state: GoLiveItem['state']): GoLiveItem => ({ key, state, missing: [], count: null, branches: null });

describe('go-live checklist', () => {
  it('counts only the lines that are needed', () => {
    const c: GoLiveChecklist = {
      slug: 'blue', name: 'Blue',
      server: [item('master_key', 'done'), item('telegram_alerts', 'missing')],
      cafe: [item('menu', 'done'), item('stripe', 'warn'), item('fiscal', 'off')],
    };
    expect(goLiveProgress(c)).toEqual({ done: 2, needed: 4 });
    expect(goLiveProgress(null)).toEqual({ done: 0, needed: 0 });
  });

  it('links every line to a guide in docs', () => {
    expect(goLiveGuide('stripe')).toBe('https://github.com/atukenov/yurt/blob/main/docs/stripe-payments.md#setting-up-a-cafe-about-10-minutes');
    expect(goLiveGuide('unknown')).toBe('https://github.com/atukenov/yurt/blob/main/docs/go-live.md');
  });

  it('shows setting names as they are', () => {
    expect(isCodeName('Stripe:WebhookSecret')).toBe(true);
    expect(isCodeName('logo')).toBe(false);
  });
});
