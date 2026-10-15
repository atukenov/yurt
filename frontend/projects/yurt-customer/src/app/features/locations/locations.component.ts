import { Component, inject, signal, effect } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { Location } from 'shared-models';
import { SkeletonCardComponent } from 'shared-ui';
import { LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { LocationService } from '../../core/location.service';
import { SwrCache } from '../../core/swr-cache.service';
import { buildScheduleInfo, ScheduleInfo } from '../../core/schedule';

@Component({
  selector: 'app-locations',
  standalone: true,
  imports: [CommonModule, SkeletonCardComponent, TranslatePipe],
  templateUrl: './locations.component.html',
  styleUrl: './locations.component.css',
})
export class LocationsComponent {
  private api = inject(YurtApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private langService = inject(LangService);
  private locationSvc = inject(LocationService);
  private swr = inject(SwrCache);

  locations = signal<Location[]>([]);
  loading = signal(true);
  selectedId = signal<string | null>(this.locationSvc.locationId() || null);

  constructor() {
    effect(() => {
      const lang = this.langService.lang();
      this.loading.set(true);
      this.swr.swr(`locs:${lang}`, () => this.api.getLocations(lang)).subscribe({
        next: (locs) => { this.locations.set(locs); this.loading.set(false); },
        error: () => this.loading.set(false),
      });
    });
  }

  scheduleOf(loc: Location): ScheduleInfo {
    return buildScheduleInfo(loc.workingHours, new Date());
  }

  select(loc: Location): void {
    this.selectedId.set(loc.id);
    this.locationSvc.setLocation(loc.id, loc.name);
  }

  confirm(): void {
    const selectedLoc = this.locations().find((l) => l.id === this.selectedId());
    if (selectedLoc) {
      this.locationSvc.setLocation(selectedLoc.id, selectedLoc.name);
    }
    // Sent here from checkout (no location chosen yet) lands back on /cart to finish
    // the order in progress, instead of dropping the customer onto the menu.
    const returnTo = this.route.snapshot.queryParamMap.get('returnTo') || '/menu';
    this.router.navigateByUrl(returnTo);
  }
}
