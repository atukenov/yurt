import { AUTO_KINDS, campaignPreset, previewText } from './guest-messages.component';

describe('guest messages', () => {
  it('offers ready texts in every language for each automatic message', () => {
    for (const kind of AUTO_KINDS) {
      const p = campaignPreset(kind);
      expect(p.title && p.titleRu && p.titleKk && p.body && p.bodyRu && p.bodyKk).toBeTruthy();
      expect(p.discountType).toBe('Percentage');
      expect(p.days).toBeGreaterThanOrEqual(kind === 'ComeBack' ? 7 : 0);
    }
    expect(campaignPreset('OneOff').segment).toBe('all');
  });

  it('previews like the server fills it in', () => {
    const p = campaignPreset('ComeBack');
    expect(previewText(p, 'ru', 'Ару', 'BACK-1')).toEqual({
      title: 'Мы скучаем, Ару!',
      body: 'Заходите на кофе: скидка 15% по коду BACK-1, действует 7 дней.',
    });
    // No name: left out cleanly. No {code} in the text: added on a new line.
    const noCode = { ...p, body: 'See you {name}', bodyRu: null, bodyKk: null };
    expect(previewText(noCode, 'en', '', 'BACK-1')).toEqual({ title: 'We miss you!', body: 'See you\nYour promo code: BACK-1' });
    // Missing Kazakh text falls back to Russian; a fixed amount shows tenge.
    const fixed = { ...p, titleKk: '', discountType: 'FixedAmount' as const, discountValue: 500 };
    expect(previewText(fixed, 'kk', 'A', 'X').title).toBe('Мы скучаем, A!');
    expect(previewText(fixed, 'kk', 'A', 'X').body).toContain('500 ₸');
  });
});
