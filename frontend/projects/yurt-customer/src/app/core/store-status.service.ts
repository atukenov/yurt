import { Injectable, computed, inject, signal } from '@angular/core';
import { LocationService } from './location.service';
import { buildScheduleInfo } from './schedule';

@Injectable({ providedIn: 'root' })
export class StoreStatusService {
  private location = inject(LocationService);

  private readonly now = signal(new Date());
  private noticeTimer?: ReturnType<typeof setTimeout>;

  /** True while the "we're closed" popup is on screen. */
  readonly noticeVisible = signal(false);

  // With a location picked, use its hours. Without one, ordering is only blocked when
  // every active location is closed (the earliest reopening time is shown).
  private readonly schedules = computed(() => {
    const now = this.now();
    if (this.location.locationId()) return [buildScheduleInfo(this.location.workingHours(), now)];
    return this.location.allWorkingHours().map((wh) => buildScheduleInfo(wh, now));
  });
  readonly isOpen = computed(() => {
    const list = this.schedules();
    return list.length === 0 || list.some((s) => s.isOpen);
  });
  readonly nextOpenAt = computed(() => {
    const times = this.schedules().map((s) => s.nextOpenAt).filter((t): t is string => !!t);
    return times.length ? times.sort()[0] : null;
  });

  constructor() {
    setInterval(() => this.now.set(new Date()), 30_000);
  }

  /** Returns true when ordering is allowed; otherwise shows the closed popup and returns false. */
  guardOpen(): boolean {
    this.now.set(new Date());
    if (this.isOpen()) return true;
    this.showNotice();
    return false;
  }

  showNotice(): void {
    this.noticeVisible.set(true);
    clearTimeout(this.noticeTimer);
    this.noticeTimer = setTimeout(() => this.dismissNotice(), 4500);
  }

  dismissNotice(): void {
    clearTimeout(this.noticeTimer);
    this.noticeVisible.set(false);
  }
}
