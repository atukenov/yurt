import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Campaign, CampaignKind, GuestSegment, GuestSegmentKey, SaveCampaign, SegmentGuest } from 'shared-models';
import { LocalDatePipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

export const AUTO_KINDS: CampaignKind[] = ['ComeBack', 'Birthday', 'FirstOrderThanks'];

export const SEGMENT_KEYS: GuestSegmentKey[] = [
  'all', 'new', 'regulars', 'sleeping_30', 'sleeping_60', 'sleeping_90', 'birthday_week', 'big_spenders', 'never_ordered',
];

type TextLang = 'ru' | 'kk' | 'en';

/** Ready-made texts for a new message, so the owner only has to check them. */
export function campaignPreset(kind: CampaignKind): SaveCampaign {
  const base = {
    kind, isActive: true, segment: null, minOrderAmount: null,
    discountType: 'Percentage' as const, codeValidDays: 7,
  };
  switch (kind) {
    case 'ComeBack':
      return {
        ...base, name: 'We miss you', days: 30, discountValue: 15,
        title: 'We miss you, {name}!', titleRu: 'Мы скучаем, {name}!', titleKk: 'Сізді сағындық, {name}!',
        body: 'Come back for coffee: {discount} off with code {code} for {days} days.',
        bodyRu: 'Заходите на кофе: скидка {discount} по коду {code}, действует {days} дней.',
        bodyKk: 'Кофеге келіңіз: {code} коды бойынша {discount} жеңілдік, {days} күн жарамды.',
      };
    case 'Birthday':
      return {
        ...base, name: 'Birthday gift', days: 3, discountValue: 20, codeValidDays: 10,
        title: 'Happy birthday, {name}!', titleRu: 'С днём рождения, {name}!', titleKk: 'Туған күніңізбен, {name}!',
        body: 'A gift from us: {discount} off with code {code}.',
        bodyRu: 'Подарок от нас: скидка {discount} по коду {code}.',
        bodyKk: 'Бізден сыйлық: {code} коды бойынша {discount} жеңілдік.',
      };
    case 'FirstOrderThanks':
      return {
        ...base, name: 'Thanks for the first order', days: 1, discountValue: 10, codeValidDays: 14,
        title: 'Thank you, {name}!', titleRu: 'Спасибо, {name}!', titleKk: 'Рахмет, {name}!',
        body: 'Glad you tried us. Your next coffee is {discount} cheaper with code {code}.',
        bodyRu: 'Рады, что вы с нами. Следующий кофе дешевле на {discount} по коду {code}.',
        bodyKk: 'Бізбен болғаныңызға қуаныштымыз. Келесі кофе {code} коды бойынша {discount} арзан.',
      };
    default:
      return {
        ...base, name: '', days: 0, segment: 'all', discountType: null, discountValue: null,
        title: '', titleRu: '', titleKk: '', body: '', bodyRu: '', bodyKk: '',
      };
  }
}

/** The message as a guest would see it, with a sample name and code (same rules as the server). */
export function previewText(c: SaveCampaign, lang: TextLang, name: string, code: string): { title: string; body: string } {
  const first = (...xs: (string | null | undefined)[]) => xs.find((x) => !!x?.trim()) ?? '';
  const title = lang === 'kk' ? first(c.titleKk, c.titleRu, c.title)
    : lang === 'en' ? first(c.title, c.titleRu, c.titleKk) : first(c.titleRu, c.title, c.titleKk);
  const body = lang === 'kk' ? first(c.bodyKk, c.bodyRu, c.body)
    : lang === 'en' ? first(c.body, c.bodyRu, c.bodyKk) : first(c.bodyRu, c.body, c.bodyKk);
  const hasCode = !!c.discountType && !!c.discountValue;
  const discount = !hasCode ? '' : c.discountType === 'Percentage' ? `${c.discountValue}%` : `${c.discountValue} ₸`;
  const fill = (t: string) => {
    let s = t;
    if (!name.trim()) s = s.replace(', {name}', '').replace(' {name}', '').replace('{name}', '');
    return s.split('{name}').join(name).split('{code}').join(hasCode ? code : '')
      .split('{discount}').join(discount).split('{days}').join(String(c.codeValidDays)).trim();
  };
  let filled = fill(body);
  if (hasCode && !body.includes('{code}')) {
    const label = lang === 'kk' ? 'Сіздің промокодыңыз' : lang === 'en' ? 'Your promo code' : 'Ваш промокод';
    filled = `${filled}\n${label}: ${code}`.trim();
  }
  return { title: fill(title), body: filled };
}

@Component({
  selector: 'app-guest-messages',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalDatePipe, LocalNumberPipe],
  templateUrl: './guest-messages.component.html',
})
export class GuestMessagesComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);
  private confirmSvc = inject(ConfirmService);

  readonly autoKinds = AUTO_KINDS;
  readonly segmentKeys = SEGMENT_KEYS;
  readonly textLangs: TextLang[] = ['ru', 'kk', 'en'];

  loading = signal(true);
  pushReady = signal(true);
  segments = signal<GuestSegment[]>([]);
  campaigns = signal<Campaign[]>([]);

  openSegment = signal<GuestSegmentKey | null>(null);
  segmentGuests = signal<SegmentGuest[]>([]);
  segmentLoading = signal(false);

  readonly oneOffs = computed(() => this.campaigns().filter((c) => c.kind === 'OneOff'));

  // Form
  editing = signal<Campaign | null>(null);
  form = signal<SaveCampaign | null>(null);
  textLang = signal<TextLang>('ru');
  saving = signal(false);
  sendingId = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getMarketingOverview().subscribe({
      next: (o) => { this.pushReady.set(o.pushReady); this.segments.set(o.segments); },
      error: () => {},
    });
    this.api.getCampaigns().subscribe({
      next: (c) => { this.campaigns.set(c); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  segment(key: GuestSegmentKey | null | undefined): GuestSegment | undefined {
    return this.segments().find((s) => s.key === key);
  }

  auto(kind: CampaignKind): Campaign | undefined {
    return this.campaigns().find((c) => c.kind === kind);
  }

  toggleSegment(key: GuestSegmentKey): void {
    if (this.openSegment() === key) { this.openSegment.set(null); return; }
    this.openSegment.set(key);
    this.segmentGuests.set([]);
    this.segmentLoading.set(true);
    this.api.getSegmentGuests(key).subscribe({
      next: (g) => { this.segmentGuests.set(g); this.segmentLoading.set(false); },
      error: () => this.segmentLoading.set(false),
    });
  }

  // ── form ──

  openNew(kind: CampaignKind, segment?: GuestSegmentKey): void {
    const preset = campaignPreset(kind);
    if (kind === 'OneOff' && segment) preset.segment = segment;
    if (kind !== 'OneOff') preset.name = this.lang.t(`gm.kind.${kind}`);
    this.editing.set(null);
    this.form.set(preset);
    this.textLang.set('ru');
  }

  openEdit(c: Campaign): void {
    const { id: _id, sentAt: _s, createdAt: _c, results: _r, ...data } = c;
    this.editing.set(c);
    this.form.set({ ...data });
    this.textLang.set('ru');
  }

  close(): void {
    this.form.set(null);
    this.editing.set(null);
  }

  patch(change: Partial<SaveCampaign>): void {
    const f = this.form();
    if (f) this.form.set({ ...f, ...change });
  }

  title(lang: TextLang): string {
    const f = this.form();
    return (lang === 'ru' ? f?.titleRu : lang === 'kk' ? f?.titleKk : f?.title) ?? '';
  }

  body(lang: TextLang): string {
    const f = this.form();
    return (lang === 'ru' ? f?.bodyRu : lang === 'kk' ? f?.bodyKk : f?.body) ?? '';
  }

  setTitle(lang: TextLang, v: string): void {
    this.patch(lang === 'ru' ? { titleRu: v } : lang === 'kk' ? { titleKk: v } : { title: v });
  }

  setBody(lang: TextLang, v: string): void {
    this.patch(lang === 'ru' ? { bodyRu: v } : lang === 'kk' ? { bodyKk: v } : { body: v });
  }

  setDiscount(type: string): void {
    if (type === 'none') this.patch({ discountType: null, discountValue: null, minOrderAmount: null });
    else this.patch({ discountType: type as 'Percentage' | 'FixedAmount', discountValue: this.form()?.discountValue || 10 });
  }

  readonly preview = computed(() => {
    const f = this.form();
    return f ? previewText(f, this.textLang(), this.lang.t('gm.sampleName'), 'BACK-7K3QX') : null;
  });

  save(): void {
    const f = this.form();
    if (!f) return;
    if (!f.name.trim()) { this.toast.warning(this.lang.t('gm.needName')); return; }
    const anyTitle = [f.title, f.titleRu, f.titleKk].some((t) => !!t?.trim());
    const anyBody = [f.body, f.bodyRu, f.bodyKk].some((t) => !!t?.trim());
    if (!anyTitle || !anyBody) { this.toast.warning(this.lang.t('gm.needText')); return; }
    this.saving.set(true);
    const edit = this.editing();
    const req$ = edit ? this.api.updateCampaign(edit.id, f) : this.api.createCampaign(f);
    req$.subscribe({
      next: () => {
        this.saving.set(false);
        this.close();
        this.toast.success(this.lang.t('gm.saved'));
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }

  toggleActive(c: Campaign): void {
    const { id, sentAt: _s, createdAt: _c, results: _r, ...data } = c;
    this.api.updateCampaign(id, { ...data, isActive: !c.isActive }).subscribe({
      next: () => { this.toast.success(this.lang.t(c.isActive ? 'gm.turnedOff' : 'gm.turnedOn')); this.load(); },
      error: () => {},
    });
  }

  async remove(c: Campaign): Promise<void> {
    if (!await this.confirmSvc.confirm(this.lang.t('gm.deleteTitle'), this.lang.t('common.deleteNamed', { name: c.name }))) return;
    this.api.deleteCampaign(c.id).subscribe({
      next: () => { this.toast.success(this.lang.t('gm.deleted')); this.load(); },
      error: () => {},
    });
  }

  async send(c: Campaign): Promise<void> {
    const reach = this.segment(c.segment)?.reachable ?? 0;
    const ok = await this.confirmSvc.confirm(
      this.lang.t('gm.sendTitle'),
      this.lang.t('gm.sendConfirm', { name: c.name, count: reach }),
      this.lang.t('gm.sendNow'),
    );
    if (!ok) return;
    this.sendingId.set(c.id);
    this.api.sendCampaign(c.id).subscribe({
      next: (r) => {
        this.sendingId.set(null);
        this.toast.success(this.lang.t('gm.sentResult', { sent: r.sent, skipped: r.skippedRecent }));
        this.load();
      },
      error: () => this.sendingId.set(null),
    });
  }
}
