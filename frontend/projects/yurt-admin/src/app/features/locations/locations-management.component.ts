import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Location, IikoTerminalGroup } from 'shared-models';
import { ButtonComponent, ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

export interface WorkingSlot {
  day: number; // 1=Mon … 7=Sun
  enabled: boolean;
  from: string; // 'HH:MM'
  to: string;   // 'HH:MM'
}

interface LocationForm {
  id?: string;
  name: string;
  address: string;
  workingSlots: WorkingSlot[];
  contactPhone: string;
  isActive: boolean;
  iikoTerminalGroupId: string;
  twoGisUrl: string;
}

const DAY_NAMES = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

const DEFAULT_SLOTS: WorkingSlot[] = DAY_NAMES.map((_, i) => ({
  day: i + 1,
  enabled: i < 5, // Mon–Fri open by default
  from: '08:00',
  to: '22:00',
}));

function parseSlots(wh: string): WorkingSlot[] {
  try {
    const p = JSON.parse(wh);
    if (Array.isArray(p) && p.length === 7) return p;
  } catch { /* legacy plain-text, fall through */ }
  return DEFAULT_SLOTS.map((s) => ({ ...s }));
}

@Component({
  selector: 'app-locations-management',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent, AdminTranslatePipe],
  templateUrl: './locations-management.component.html',
  styleUrl: './locations-management.component.css',
})
export class LocationsManagementComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private confirmSvc = inject(ConfirmService);

  readonly DAY_NAMES = DAY_NAMES;

  locations = signal<Location[]>([]);
  loading = signal(true);
  saving = signal(false);
  showDialog = signal(false);
  form = signal<LocationForm>({
    name: '',
    address: '',
    workingSlots: DEFAULT_SLOTS.map((s) => ({ ...s })),
    contactPhone: '',
    isActive: true,
    iikoTerminalGroupId: '',
    twoGisUrl: '',
  });

  // iiko terminal groups for the routing picker (Phase 3 order-push feature).
  iikoTerminalGroups = signal<IikoTerminalGroup[]>([]);

  ngOnInit(): void {
    this.api.getAdminLocations().subscribe({
      next: (locs) => { this.locations.set(locs); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.getIikoTerminalGroups().subscribe({
      next: (groups) => this.iikoTerminalGroups.set(groups),
      error: () => this.iikoTerminalGroups.set([]), // iiko disabled/unreachable — picker just stays empty
    });
  }

  openDialog(loc?: Location): void {
    this.form.set({
      id: loc?.id,
      name: loc?.name ?? '',
      address: loc?.address ?? '',
      workingSlots: parseSlots(loc?.workingHours ?? ''),
      contactPhone: loc?.contactPhone ?? '',
      isActive: loc?.isActive ?? true,
      iikoTerminalGroupId: loc?.iikoTerminalGroupId ?? '',
      twoGisUrl: loc?.twoGisUrl ?? '',
    });
    this.showDialog.set(true);
  }

  patch(field: keyof Omit<LocationForm, 'workingSlots'>, value: unknown): void {
    this.form.update((f) => ({ ...f, [field]: value }));
  }

  toggleDay(index: number): void {
    this.form.update((f) => ({
      ...f,
      workingSlots: f.workingSlots.map((s, i) =>
        i === index ? { ...s, enabled: !s.enabled } : s
      ),
    }));
  }

  updateSlotTime(index: number, field: 'from' | 'to', value: string): void {
    this.form.update((f) => ({
      ...f,
      workingSlots: f.workingSlots.map((s, i) =>
        i === index ? { ...s, [field]: value } : s
      ),
    }));
  }

  formatHours(wh: string): string {
    try {
      const slots: WorkingSlot[] = JSON.parse(wh);
      if (!Array.isArray(slots)) return wh;
      const open = slots.filter((s) => s.enabled);
      if (!open.length) return 'Closed';
      const allSame = open.every((s) => s.from === open[0].from && s.to === open[0].to);
      const names = open.map((s) => DAY_NAMES[s.day - 1]);
      const timeStr = `${open[0].from}–${open[0].to}`;
      if (open.length === 7) return allSame ? `Daily ${timeStr}` : 'Daily (varied hours)';
      return allSame ? `${names.join(', ')} ${timeStr}` : `${open.length} days open`;
    } catch {
      return wh;
    }
  }

  save(): void {
    const f = this.form();
    if (!f.name || !f.address) {
      this.toast.error('Name and address are required');
      return;
    }
    this.saving.set(true);
    const payload: Partial<Location> = {
      name: f.name,
      address: f.address,
      workingHours: JSON.stringify(f.workingSlots),
      contactPhone: f.contactPhone,
      isActive: f.isActive,
      iikoTerminalGroupId: f.iikoTerminalGroupId || null,
      twoGisUrl: f.twoGisUrl.trim() || null,
    };
    const obs = f.id
      ? this.api.updateLocation(f.id, payload)
      : this.api.createLocation(payload);

    obs.subscribe({
      next: (loc) => {
        if (f.id) {
          this.locations.update((list) => list.map((l) => (l.id === loc.id ? loc : l)));
        } else {
          this.locations.update((list) => [...list, loc]);
        }
        this.saving.set(false);
        this.showDialog.set(false);
        this.toast.success('Location saved');
      },
      error: () => this.saving.set(false),
    });
  }

  async deleteLocation(loc: Location): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Location', `Delete "${loc.name}"?`)) return;
    this.api.deleteLocation(loc.id).subscribe({
      next: () => {
        this.locations.update((list) => list.filter((l) => l.id !== loc.id));
        this.toast.success('Location deleted');
      },
      error: () => {},
    });
  }
}
