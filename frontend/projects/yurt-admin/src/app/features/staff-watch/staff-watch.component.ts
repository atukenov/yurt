import { Component, computed, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { SignalrService, YurtApiService } from 'shared-api';
import { Location, WatchBox, WatchCamera, WatchDay, WatchLive, WatchSettings, WatchShift, WatchZone, WatchZoneKind } from 'shared-models';
import { LocalDatePipe, ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';

export type WatchTab = 'day' | 'cameras' | 'shifts' | 'settings' | 'box';

/** The cafe's local date as yyyy-MM-dd (Almaty, like the server). */
export function cafeToday(now = new Date()): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Almaty' }).format(now);
}

/** A free zone name like "station2", not used by any zone of the branch. */
export function nextZoneName(kind: WatchZoneKind, taken: string[]): string {
  const used = new Set(taken);
  for (let n = 1; ; n++) if (!used.has(`${kind}${n}`)) return `${kind}${n}`;
}

/** A point from a click on the picture, in percent, rounded to 0.1. */
export function percentPoint(clientX: number, clientY: number, rect: { left: number; top: number; width: number; height: number }): number[] {
  const clamp = (v: number) => Math.min(100, Math.max(0, Math.round(v * 10) / 10));
  return [clamp(((clientX - rect.left) / rect.width) * 100), clamp(((clientY - rect.top) / rect.height) * 100)];
}

/** The box checks in every minute; after 3 missed minutes it counts as offline (as on the server). */
export const BOX_ONLINE_MS = 3 * 60_000;

/** Seconds from a cafe-local time like "2026-11-12T10:05:20" to `now` (which is also cafe-local). */
export function secondsSince(localIso: string | null | undefined, nowLocalMs: number): number {
  if (!localIso) return 0;
  const [d, t] = localIso.split('T');
  const [y, mo, da] = d.split('-').map(Number);
  const [h, mi, s] = (t ?? '0:0:0').split(':').map(Number);
  return Math.max(0, Math.floor((nowLocalMs - Date.UTC(y, mo - 1, da, h, mi, s || 0)) / 1000));
}

/** 75 -> "1:15", 3725 -> "1:02:05". */
export function clock(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  const mm = h ? String(m).padStart(2, '0') : String(m);
  return `${h ? h + ':' : ''}${mm}:${String(s).padStart(2, '0')}`;
}

/** Now in Almaty as if it were UTC, to compare with the box's local times. */
export function cafeNowMs(now = new Date()): number {
  return now.getTime() + 5 * 3600_000; // Kazakhstan is UTC+5 all year
}

const DAYS = ['daily', 'mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'];

/**
 * Staff watch (`staff_watch`): a small box in the cafe looks at the cameras and sends counts per minute. Here the owner
 * reads the day (visitors vs orders, alerts, minutes at each station), draws the zones on a still picture of each
 * camera, keeps the shift plan, sets alerts and Telegram, and makes the key the box signs in with.
 */
@Component({
  selector: 'app-staff-watch',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalDatePipe],
  templateUrl: './staff-watch.component.html',
})
export class StaffWatchComponent implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);
  private signalr = inject(SignalrService);

  readonly days = DAYS;
  readonly cameraExample = 'bar';
  readonly tokenExample = '123456789:AA…';
  readonly tabs: WatchTab[] = ['day', 'cameras', 'shifts', 'settings', 'box'];

  locations = signal<Location[]>([]);
  locationId = signal('');
  tab = signal<WatchTab>('day');
  busy = signal<string | null>(null);

  // the day
  day = signal(cafeToday());
  dayData = signal<WatchDay | null>(null);
  readonly maxHour = computed(() => Math.max(1, ...(this.dayData()?.hours ?? []).map((h) => Math.max(h.visitors, h.orders))));

  // the box
  boxes = signal<WatchBox[]>([]);
  newKey = signal<string | null>(null);
  readonly box = computed(() => this.boxes().find((b) => b.locationId === this.locationId()) ?? null);
  readonly boxOnline = computed(() => {
    const seen = this.box()?.lastSeenAt;
    return !!seen && Date.now() - new Date(seen).getTime() < BOX_ONLINE_MS;
  });

  // right now (live panel on the Day tab for today)
  live = signal<WatchLive | null>(null);
  nowMs = signal(cafeNowMs());
  alertPictures = signal<Record<string, string>>({});
  readonly isToday = computed(() => this.day() === cafeToday());
  readonly liveOnline = computed(() => {
    const l = this.live();
    if (!l?.online || !l.lastSeenAt) return false;
    this.nowMs();
    return Date.now() - new Date(l.lastSeenAt).getTime() < BOX_ONLINE_MS;
  });
  private liveSub?: Subscription;
  private watchedLocation: string | null = null;
  private liveTimer?: ReturnType<typeof setInterval>;
  private livePoll?: ReturnType<typeof setInterval>;

  // cameras and zones
  cameras = signal<WatchCamera[]>([]);
  cameraId = signal<string | null>(null);
  zones = signal<WatchZone[]>([]);
  dirty = signal(false);
  pictures = signal<Record<string, string>>({});
  drawKind = signal<WatchZoneKind | null>(null);
  draft = signal<number[][]>([]);
  draftInside = signal<number[] | null>(null);
  newCamera = '';
  readonly camera = computed(() => this.cameras().find((c) => c.id === this.cameraId()) ?? null);
  readonly picture = computed(() => {
    const id = this.cameraId();
    return id ? this.pictures()[id] ?? null : null;
  });
  readonly aspect = computed(() => {
    const c = this.camera();
    return c && c.snapshotWidth > 0 && c.snapshotHeight > 0 ? `${c.snapshotWidth} / ${c.snapshotHeight}` : '16 / 9';
  });
  /** Zone names on the other cameras of this branch: names must differ across the branch. */
  readonly otherZones = computed(() =>
    this.cameras().filter((c) => c.id !== this.cameraId()).flatMap((c) => c.zones),
  );
  readonly queueChoices = computed(() => [...this.zones(), ...this.otherZones()].filter((z) => z.kind === 'queue'));
  readonly stationChoices = computed(() => {
    const own = this.cameraId() ? this.zones() : [];
    return [...own, ...this.otherZones()].filter((z) => z.kind === 'station');
  });
  readonly draftDone = computed(() => {
    const kind = this.drawKind();
    if (kind === 'door') return this.draft().length === 2 && !!this.draftInside();
    return this.draft().length >= 3;
  });

  // shifts
  shifts = signal<WatchShift[]>([]);
  readonly allStations = computed(() => this.cameras().flatMap((c) => c.zones).filter((z) => z.kind === 'station'));

  // settings
  settings = signal<WatchSettings | null>(null);
  botToken = '';

  private refreshTimer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe((locs) => {
      this.locations.set(locs);
      if (locs.length) this.pick(locs[0].id);
    });
    this.loadBoxes();
    // A new still picture arrives within about a minute of asking; look again while one is awaited.
    this.refreshTimer = setInterval(() => {
      if (this.tab() === 'cameras' && this.cameras().some((c) => this.waiting(c))) this.loadCameras(false);
    }, 15_000);
    // live: the box pushes changes through SignalR; a slow poll covers a dropped connection
    this.liveSub = this.signalr.watchLive$.subscribe((l) => {
      if (l.locationId === this.locationId()) this.live.set(l);
    });
    this.signalr
      .startConnection()
      .then(() => {
        // a branch picked before the connection was up: join its group now
        if (this.watchedLocation) return this.signalr.subscribeToWatch(this.watchedLocation);
        this.watchLive();
        return undefined;
      })
      .catch(() => undefined);
    this.liveTimer = setInterval(() => this.nowMs.set(cafeNowMs()), 1000);
    this.livePoll = setInterval(() => {
      if (this.tab() === 'day' && this.isToday()) this.loadLive();
    }, 60_000);
  }

  ngOnDestroy(): void {
    clearInterval(this.refreshTimer);
    clearInterval(this.liveTimer);
    clearInterval(this.livePoll);
    this.liveSub?.unsubscribe();
    if (this.watchedLocation) this.signalr.unsubscribeFromWatch(this.watchedLocation).catch(() => undefined);
    Object.values(this.pictures()).forEach((u) => URL.revokeObjectURL(u));
    Object.values(this.alertPictures()).forEach((u) => URL.revokeObjectURL(u));
  }

  // ── right now ─────────────────────────────────────────────────────────

  private watchLive(): void {
    const id = this.locationId();
    if (!id || id === this.watchedLocation) return;
    if (this.watchedLocation) this.signalr.unsubscribeFromWatch(this.watchedLocation).catch(() => undefined);
    this.watchedLocation = id;
    this.signalr.subscribeToWatch(id).catch(() => undefined);
  }

  loadLive(): void {
    const id = this.locationId();
    if (!id) return;
    this.api.getWatchLive(id).subscribe({
      next: (l) => this.live.set(l),
      error: () => this.live.set(null),
    });
  }

  since(localIso: string | null | undefined): string {
    return clock(secondsSince(localIso, this.nowMs()));
  }

  toggleAlertPicture(id: string): void {
    const shown = this.alertPictures();
    if (shown[id]) {
      URL.revokeObjectURL(shown[id]);
      const { [id]: _, ...rest } = shown;
      this.alertPictures.set(rest);
      return;
    }
    this.api.getWatchAlertPicture(id).subscribe({
      next: (blob) => this.alertPictures.update((p) => ({ ...p, [id]: URL.createObjectURL(blob) })),
      error: () => this.toast.error(this.lang.t('watch.day.noPicture')),
    });
  }

  pick(id: string): void {
    this.locationId.set(id);
    this.newKey.set(null);
    this.cameraId.set(null);
    this.cancelDraw();
    this.live.set(null);
    this.loadAll();
    this.loadLive();
    this.watchLive();
  }

  show(tab: WatchTab): void {
    this.tab.set(tab);
    if (tab === 'day') this.loadDay();
    if (tab === 'box') this.loadBoxes();
  }

  private loadAll(): void {
    this.loadDay();
    this.loadCameras(true);
    const id = this.locationId();
    this.api.getWatchShifts(id).subscribe((rows) => this.shifts.set(rows));
    this.api.getWatchSettings(id).subscribe((s) => this.settings.set(s));
  }

  // ── the day ───────────────────────────────────────────────────────────

  loadDay(): void {
    const id = this.locationId();
    if (!id) return;
    this.api.getWatchDay(id, this.day()).subscribe({
      next: (d) => this.dayData.set(d),
      error: () => this.dayData.set(null),
    });
  }

  pickDay(day: string): void {
    this.day.set(day || cafeToday());
    this.loadDay();
  }

  /** Visitors who did not order, as a share of all visitors; null when nobody was counted. */
  noOrderShare(d: WatchDay): number | null {
    if (!d.visitors) return null;
    return Math.max(0, Math.round(((d.visitors - d.orders) / d.visitors) * 100));
  }

  stationLabel(zone: string): string {
    return this.allStations().find((z) => z.name === zone)?.label || zone;
  }

  share(part: number, whole: number): number {
    return whole > 0 ? Math.round((part / whole) * 100) : 0;
  }

  time(at: string): string {
    return at.length >= 16 ? at.substring(11, 16) : at;
  }

  // ── box ───────────────────────────────────────────────────────────────

  loadBoxes(): void {
    this.api.getWatchBoxes().subscribe((b) => this.boxes.set(b));
  }

  makeKey(): void {
    if (this.box()?.hasKey && !confirm(this.lang.t('watch.box.confirmNewKey'))) return;
    this.busy.set('key');
    this.api.newWatchKey(this.locationId()).subscribe({
      next: (k) => {
        this.busy.set(null);
        this.newKey.set(k.key);
        this.loadBoxes();
      },
      error: () => this.busy.set(null),
    });
  }

  revokeKey(): void {
    if (!confirm(this.lang.t('watch.box.confirmRevoke'))) return;
    this.api.revokeWatchKey(this.locationId()).subscribe(() => {
      this.newKey.set(null);
      this.loadBoxes();
    });
  }

  /** The part of the box's config.toml that connects it to this server. */
  boxConfig(key: string): string {
    return `[yurt]\napi = "${this.api.serverUrl}"\nkey = "${key}"`;
  }

  copy(text: string): void {
    navigator.clipboard?.writeText(text).then(
      () => this.toast.success(this.lang.t('watch.copied')),
      () => undefined,
    );
  }

  // ── cameras ───────────────────────────────────────────────────────────

  loadCameras(selectFirst: boolean): void {
    const id = this.locationId();
    if (!id) return;
    this.api.getWatchCameras(id).subscribe((list) => {
      const before = new Map(this.cameras().map((c) => [c.id, c.snapshotAt]));
      this.cameras.set(list);
      for (const c of list) if (c.snapshotAt && before.get(c.id) !== c.snapshotAt) this.loadPicture(c.id);
      const current = list.find((c) => c.id === this.cameraId());
      if (current) {
        if (!this.dirty()) this.zones.set(structuredClone(current.zones));
      } else if (selectFirst || this.cameraId()) {
        this.selectCamera(list[0]?.id ?? null);
      }
    });
  }

  private loadPicture(id: string): void {
    this.api.getWatchSnapshot(id).subscribe({
      next: (blob) => {
        const old = this.pictures()[id];
        if (old) URL.revokeObjectURL(old);
        this.pictures.update((p) => ({ ...p, [id]: URL.createObjectURL(blob) }));
      },
      error: () => undefined,
    });
  }

  selectCamera(id: string | null): void {
    if (this.dirty() && !confirm(this.lang.t('watch.cam.confirmLeave'))) return;
    this.cameraId.set(id);
    this.zones.set(structuredClone(this.cameras().find((c) => c.id === id)?.zones ?? []));
    this.dirty.set(false);
    this.cancelDraw();
  }

  /** A picture is on its way: asked for, and nothing newer has arrived yet. */
  waiting(c: WatchCamera): boolean {
    if (!c.snapshotRequestedAt) return false;
    return !c.snapshotAt || new Date(c.snapshotAt) < new Date(c.snapshotRequestedAt);
  }

  addCamera(): void {
    const name = this.newCamera.trim().toLowerCase();
    if (!name) return;
    this.busy.set('camera');
    this.api.createWatchCamera(this.locationId(), name).subscribe({
      next: (c) => {
        this.busy.set(null);
        this.newCamera = '';
        this.cameras.update((l) => [...l, c]);
        this.selectCamera(c.id);
      },
      error: () => this.busy.set(null),
    });
  }

  deleteCamera(c: WatchCamera): void {
    if (!confirm(this.lang.t('watch.cam.confirmDelete', { name: c.name }))) return;
    this.api.deleteWatchCamera(c.id).subscribe(() => {
      this.dirty.set(false);
      this.cameras.update((l) => l.filter((x) => x.id !== c.id));
      this.selectCamera(this.cameras()[0]?.id ?? null);
    });
  }

  newPicture(c: WatchCamera): void {
    this.api.requestWatchSnapshot(c.id).subscribe((saved) => {
      this.cameras.update((l) => l.map((x) => (x.id === saved.id ? saved : x)));
      this.toast.info(this.lang.t('watch.cam.pictureAsked'));
    });
  }

  startDraw(kind: WatchZoneKind): void {
    this.drawKind.set(kind);
    this.draft.set([]);
    this.draftInside.set(null);
  }

  cancelDraw(): void {
    this.drawKind.set(null);
    this.draft.set([]);
    this.draftInside.set(null);
  }

  /** A click on the picture adds a corner; for a door: two ends of the line, then a point on the inside. */
  clickPicture(event: MouseEvent): void {
    const kind = this.drawKind();
    if (!kind) return;
    const el = event.currentTarget as Element;
    const p = percentPoint(event.clientX, event.clientY, el.getBoundingClientRect());
    if (kind === 'door') {
      if (this.draft().length < 2) this.draft.update((d) => [...d, p]);
      else this.draftInside.set(p);
      if (this.draftDone()) this.finishDraw();
      return;
    }
    if (this.draft().length >= 20) return;
    this.draft.update((d) => [...d, p]);
  }

  undoPoint(): void {
    if (this.draftInside()) this.draftInside.set(null);
    else this.draft.update((d) => d.slice(0, -1));
  }

  finishDraw(): void {
    const kind = this.drawKind();
    if (!kind || !this.draftDone()) return;
    const taken = [...this.zones(), ...this.otherZones()].map((z) => z.name);
    const zone: WatchZone = { name: nextZoneName(kind, taken), label: '', kind, points: this.draft() };
    if (kind === 'door') zone.inside = this.draftInside();
    this.zones.update((z) => [...z, zone]);
    this.dirty.set(true);
    this.cancelDraw();
  }

  removeZone(i: number): void {
    const gone = this.zones()[i];
    this.zones.update((z) => z.filter((_, j) => j !== i).map((x) => (x.queue === gone.name ? { ...x, queue: null } : x)));
    this.dirty.set(true);
  }

  touch(): void {
    this.dirty.set(true);
  }

  saveZones(): void {
    const c = this.camera();
    if (!c) return;
    this.busy.set('zones');
    this.api.saveWatchCamera({ ...c, zones: this.zones() }).subscribe({
      next: (saved) => {
        this.busy.set(null);
        this.dirty.set(false);
        this.cameras.update((l) => l.map((x) => (x.id === saved.id ? saved : x)));
        this.zones.set(structuredClone(saved.zones));
        this.toast.success(this.lang.t('watch.saved'));
      },
      error: () => this.busy.set(null),
    });
  }

  polygon(points: number[][]): string {
    return points.map((p) => `${p[0]},${p[1]}`).join(' ');
  }

  /** Where to write a zone's name on the picture: the middle of its corners. */
  middle(points: number[][]): number[] {
    if (!points.length) return [0, 0];
    return [points.reduce((s, p) => s + p[0], 0) / points.length, points.reduce((s, p) => s + p[1], 0) / points.length];
  }

  zoneColor(kind: WatchZoneKind): string {
    return kind === 'station' ? '#16a34a' : kind === 'queue' ? '#f59e0b' : '#2563eb';
  }

  // ── shifts ────────────────────────────────────────────────────────────

  addShift(): void {
    const station = this.allStations()[0]?.name ?? '';
    this.shifts.update((s) => [...s, { day: 'daily', name: '', station, start: '09:00', end: '18:00' }]);
  }

  removeShift(i: number): void {
    this.shifts.update((s) => s.filter((_, j) => j !== i));
  }

  saveShifts(): void {
    this.busy.set('shifts');
    const rows = this.shifts().map((r) => ({ ...r, name: r.name.trim(), day: r.day.trim() }));
    this.api.saveWatchShifts(this.locationId(), rows).subscribe({
      next: (saved) => {
        this.busy.set(null);
        this.shifts.set(saved);
        this.toast.success(this.lang.t('watch.saved'));
      },
      error: () => this.busy.set(null),
    });
  }

  // ── settings ──────────────────────────────────────────────────────────

  saveSettings(): void {
    const s = this.settings();
    if (!s) return;
    this.busy.set('settings');
    this.api.saveWatchSettings(s).subscribe({
      next: (saved) => {
        this.busy.set(null);
        this.settings.set(saved);
        this.toast.success(this.lang.t('watch.saved'));
      },
      error: () => this.busy.set(null),
    });
  }

  saveBot(): void {
    const token = this.botToken.trim();
    if (!token && !confirm(this.lang.t('watch.tg.confirmRemove'))) return;
    this.busy.set('bot');
    this.api.setWatchTelegramToken(token || null).subscribe({
      next: () => {
        this.busy.set(null);
        this.botToken = '';
        this.settings.update((s) => (s ? { ...s, telegramBotSet: !!token } : s));
        this.toast.success(this.lang.t('watch.saved'));
      },
      error: () => this.busy.set(null),
    });
  }

  findChat(): void {
    this.busy.set('chat');
    this.api.findWatchTelegramChat(this.locationId()).subscribe({
      next: (s) => {
        this.busy.set(null);
        this.settings.set(s);
        this.toast.success(this.lang.t('watch.tg.found'));
      },
      error: () => this.busy.set(null),
    });
  }

  testChat(): void {
    this.busy.set('test');
    this.api.testWatchTelegram(this.locationId()).subscribe({
      next: () => {
        this.busy.set(null);
        this.toast.success(this.lang.t('watch.tg.sent'));
      },
      error: () => this.busy.set(null),
    });
  }
}
