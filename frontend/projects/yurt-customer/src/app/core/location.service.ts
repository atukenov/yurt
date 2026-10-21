import { Injectable, signal, effect, inject } from '@angular/core';
import { YurtApiService } from 'shared-api';
import { LangService } from './lang.service';
import { SwrCache } from './swr-cache.service';

function readJson(key: string): string[] | null {
  try {
    const v = JSON.parse(localStorage.getItem(key) ?? 'null');
    return Array.isArray(v) ? v : null;
  } catch {
    return null;
  }
}

@Injectable({ providedIn: 'root' })
export class LocationService {
  private api = inject(YurtApiService);
  private langService = inject(LangService);
  private swr = inject(SwrCache);

  readonly locationId = signal(localStorage.getItem('yurt_location_id') ?? '');
  // Seeded from cache so the name shows instantly on startup; refreshed from API below
  readonly locationName = signal(localStorage.getItem('yurt_location_name') ?? '');

  // Raw working-hours string of the selected location (JSON slots); '' = unknown
  readonly workingHours = signal(localStorage.getItem('yurt_location_hours') ?? '');

  // Payment methods the selected branch offers; null = all (own POS can narrow it per branch)
  readonly paymentMethods = signal<string[] | null>(readJson('yurt_location_pay'));

  // Working hours of every active location — used when the customer hasn't picked one yet
  readonly allWorkingHours = signal<string[]>([]);

  constructor() {
    effect(() => {
      const id = this.locationId();
      const lang = this.langService.lang();
      if (!id) {
        this.locationName.set('');
        this.workingHours.set('');
      }
      this.swr.swr(`locs:${lang}`, () => this.api.getLocations(lang)).subscribe({
        next: (locs) => {
          this.allWorkingHours.set(locs.filter((l) => l.isActive).map((l) => l.workingHours ?? ''));
          if (!id) return;
          const loc = locs.find((l) => l.id === id);
          if (loc) {
            this.paymentMethods.set(loc.paymentMethods?.length ? loc.paymentMethods : null);
            localStorage.setItem('yurt_location_pay', JSON.stringify(this.paymentMethods()));
            this.workingHours.set(loc.workingHours ?? '');
            localStorage.setItem('yurt_location_hours', loc.workingHours ?? '');
          }
          if (loc && loc.name) {
            this.locationName.set(loc.name);
            localStorage.setItem('yurt_location_name', loc.name);
          }
        },
        error: () => {},
      });
    });
  }

  setLocation(id: string, name: string): void {
    localStorage.setItem('yurt_location_id', id);
    localStorage.setItem('yurt_location_name', name);
    this.locationId.set(id);
    this.locationName.set(name); // optimistic; effect re-fetches with fresh DB data
  }
}
