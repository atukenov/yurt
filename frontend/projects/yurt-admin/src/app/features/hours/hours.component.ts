import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ModuleService, YurtApiService } from 'shared-api';
import { ClockCrossCheck, HoursReport, Location, StaffHours, TimeEntry } from 'shared-models';
import { LocalDatePipe, LocalNumberPipe, ToastService } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { dayText, monthStart } from '../tips/tips.component';

/**
 * Worked hours (Admin and CEO, module own_pos): hours per person from the till's clock-in and clock-out, every entry,
 * and a way to fix a forgotten clock-out or add a missed shift. Every change needs a reason and is written to the
 * audit log. For people with an hourly rate the hours go into payroll.
 */
@Component({
  selector: 'app-hours',
  standalone: true,
  imports: [FormsModule, AdminTranslatePipe, LocalDatePipe, LocalNumberPipe],
  template: `
    <div class="p-4 md:p-6 max-w-5xl mx-auto space-y-5">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">{{ 'hours.title' | t }}</h1>
        <p class="text-slate-500 text-sm mt-0.5">{{ 'hours.hint' | t }}</p>
      </div>

      <div class="flex flex-wrap items-center gap-3">
        <label class="text-sm font-semibold text-slate-700" for="hours-from">{{ 'tips.from' | t }}</label>
        <input id="hours-from" type="date" [ngModel]="from()" (ngModelChange)="from.set($event); load()"
          class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
        <label class="text-sm font-semibold text-slate-700" for="hours-to">{{ 'tips.to' | t }}</label>
        <input id="hours-to" type="date" [ngModel]="to()" (ngModelChange)="to.set($event); load()"
          class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
      </div>

      @if (!report()) {
        <div class="h-40 bg-slate-100 rounded-2xl animate-pulse"></div>
      } @else {
        @let r = report()!;
        <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
          <h2 class="font-semibold text-slate-800 mb-3">{{ 'hours.byStaff' | t }} · {{ r.totalHours | lnumber: '1.0-1' }} {{ 'hours.h' | t }}</h2>
          @if (!r.byStaff.length) {
            <p class="text-sm text-slate-500">{{ 'hours.empty' | t }}</p>
          } @else {
            <table class="w-full text-sm">
              <thead>
                <tr class="border-b border-slate-100 text-slate-500 text-xs uppercase tracking-wide">
                  <th class="text-left px-3 py-2">{{ 'payroll.person' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'hours.hours' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'hours.days' | t }}</th>
                  <th class="text-right px-3 py-2">{{ 'hours.problems' | t }}</th>
                </tr>
              </thead>
              <tbody>
                @for (s of r.byStaff; track s.adminUserId) {
                  <tr class="border-b border-slate-50">
                    <td class="px-3 py-2"><span class="font-medium text-slate-800">{{ s.username }}</span>
                      <span class="block text-xs text-slate-500">{{ ('role.' + s.role) | t }}</span></td>
                    <td class="px-3 py-2 text-right font-semibold">{{ s.hours | lnumber: '1.0-2' }}</td>
                    <td class="px-3 py-2 text-right">{{ s.days }}</td>
                    <td class="px-3 py-2 text-right" [class]="s.problems ? 'text-tone-red font-semibold' : 'text-slate-400'">{{ s.problems }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>

        <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
          <h2 class="font-semibold text-slate-800 mb-3">{{ 'hours.entries' | t }}</h2>
          @for (e of r.entries; track e.id) {
            <div class="border-b border-slate-50 py-2 text-sm">
              <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
                <span class="w-32 font-medium text-slate-800">{{ e.username }}</span>
                <span class="text-slate-600">{{ e.clockInAt | ldate: 'shortDateTime24' }} →
                  @if (e.clockOutAt && !e.forgotten) { {{ e.clockOutAt | ldate: 'shortDateTime24' }} }
                  @else if (e.open) { <em>{{ 'hours.open' | t }}</em> }
                  @else { <strong class="text-tone-red">{{ 'hours.forgotten' | t }}</strong> }
                </span>
                <span class="font-semibold">{{ e.hours | lnumber: '1.0-2' }} {{ 'hours.h' | t }}</span>
                <span class="flex-1"></span>
                <button type="button" (click)="startEdit(e)" class="underline text-slate-600">{{ 'action.edit' | t }}</button>
                <button type="button" (click)="startDelete(e)" class="underline text-slate-600">{{ 'action.delete' | t }}</button>
              </div>
              @if (e.editedBy) {
                <p class="text-xs text-slate-500">{{ 'hours.editedBy' | t }} {{ e.editedBy }}: {{ e.editNote }}</p>
              }
              @if (editing()?.id === e.id) {
                <form class="mt-2 grid gap-2 md:grid-cols-4" (ngSubmit)="saveEdit()">
                  <input type="datetime-local" name="in" [(ngModel)]="editIn" [attr.aria-label]="'hours.in' | t" class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
                  <input type="datetime-local" name="out" [(ngModel)]="editOut" [attr.aria-label]="'hours.out' | t" class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
                  <input type="text" name="reason" maxlength="200" [(ngModel)]="reason" [placeholder]="'hours.reason' | t" class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
                  <button type="submit" [disabled]="busy() || !reason.trim() || !editIn || !editOut"
                    class="px-3 py-1.5 rounded-xl bg-brand-500 text-on-brand text-sm font-semibold disabled:opacity-50">{{ 'action.save' | t }}</button>
                </form>
              }
              @if (deleting()?.id === e.id) {
                <form class="mt-2 flex flex-wrap gap-2" (ngSubmit)="confirmDelete()">
                  <input type="text" name="dreason" maxlength="200" [(ngModel)]="reason" [placeholder]="'hours.reason' | t" class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm flex-1" />
                  <button type="submit" [disabled]="busy() || !reason.trim()"
                    class="px-3 py-1.5 rounded-xl bg-red-600 text-white text-sm font-semibold disabled:opacity-50">{{ 'action.delete' | t }}</button>
                </form>
              }
            </div>
          }
        </section>

        @if (cameraOn) {
          <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
            <h2 class="font-semibold text-slate-800">{{ 'hours.camera' | t }}</h2>
            <p class="text-xs text-slate-500 mb-3">{{ 'hours.cameraHint' | t }}</p>
            <div class="flex flex-wrap items-center gap-3 mb-3">
              <input type="date" [ngModel]="cameraDay()" (ngModelChange)="cameraDay.set($event); loadCamera()" [attr.aria-label]="'hours.cameraDay' | t"
                class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm" />
              @if (locations().length > 1) {
                <select [ngModel]="cameraBranch()" (ngModelChange)="cameraBranch.set($event); loadCamera()" [attr.aria-label]="'tips.branch' | t"
                  class="px-3 py-1.5 rounded-xl border border-slate-200 text-sm">
                  @for (l of locations(); track l.id) { <option [value]="l.id">{{ l.name }}</option> }
                </select>
              }
            </div>
            @if (camera(); as c) {
              @if (!c.hasCameras) {
                <p class="text-sm text-slate-500">{{ 'hours.cameraNone' | t }}</p>
              } @else if (!c.cameraMinutes) {
                <p class="text-sm text-slate-500">{{ 'hours.cameraNoData' | t }}</p>
              } @else if (!c.findings.length) {
                <p class="text-sm text-green-700">{{ 'hours.cameraOk' | t }}</p>
              } @else {
                @for (f of c.findings; track f.from + f.kind) {
                  <div class="border-b border-slate-50 py-2 text-sm">
                    <p class="font-medium" [class]="f.kind === 'clocked_not_seen' ? 'text-slate-700' : 'text-tone-red'">
                      {{ ('hours.cam.' + f.kind) | t }}
                    </p>
                    <p class="text-slate-600">
                      {{ f.from | ldate: 'time24' }}–{{ f.to | ldate: 'time24' }} · {{ f.minutes }} {{ 'hours.min' | t }}
                      · {{ 'hours.camSeen' | t }} {{ f.maxSeen }}, {{ 'hours.camClocked' | t }} {{ f.maxClocked }}
                    </p>
                  </div>
                }
              }
            }
          </section>
        }

        <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
          <h2 class="font-semibold text-slate-800 mb-3">{{ 'hours.add' | t }}</h2>
          <form class="grid gap-3 md:grid-cols-5" (ngSubmit)="add()">
            <select name="person" [(ngModel)]="addUser" [attr.aria-label]="'payroll.person' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm">
              <option value="">{{ 'payroll.person' | t }}</option>
              @for (p of people(); track p.adminUserId) {
                <option [value]="p.adminUserId">{{ p.username }}</option>
              }
            </select>
            <input type="datetime-local" name="addIn" [(ngModel)]="addIn" [attr.aria-label]="'hours.in' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            <input type="datetime-local" name="addOut" [(ngModel)]="addOut" [attr.aria-label]="'hours.out' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            <input type="text" name="addReason" maxlength="200" [(ngModel)]="addReason" [placeholder]="'hours.reason' | t" class="px-3 py-2 rounded-xl border border-slate-200 text-sm" />
            <button type="submit" [disabled]="busy() || !addUser || !addIn || !addOut || !addReason.trim()"
              class="px-4 py-2 rounded-xl bg-brand-500 text-on-brand text-sm font-semibold disabled:opacity-50">{{ 'action.add' | t }}</button>
          </form>
        </section>
      }
    </div>
  `,
})
export class HoursComponent implements OnInit {
  private api = inject(YurtApiService);
  private modules = inject(ModuleService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  /** Camera check: only when Yurt Watch is on. */
  readonly cameraOn = this.modules.isEnabled('staff_watch');
  locations = signal<Location[]>([]);
  cameraBranch = signal('');
  cameraDay = signal(dayText(new Date()));
  camera = signal<ClockCrossCheck | null>(null);

  from = signal(monthStart(new Date()));
  to = signal(dayText(new Date()));
  report = signal<HoursReport | null>(null);
  /** Who can be picked for a missed shift. */
  people = signal<StaffHours[]>([]);
  busy = signal(false);

  editing = signal<TimeEntry | null>(null);
  deleting = signal<TimeEntry | null>(null);
  editIn = '';
  editOut = '';
  reason = '';

  addUser = '';
  addIn = '';
  addOut = '';
  addReason = '';

  ngOnInit(): void {
    this.load();
    if (this.cameraOn) {
      this.api.getAdminLocations().subscribe({
        next: (l) => {
          this.locations.set(l);
          if (l.length) this.cameraBranch.set(l[0].id);
          this.loadCamera();
        },
        error: () => {},
      });
    }
  }

  loadCamera(): void {
    if (!this.cameraBranch() || !this.cameraDay()) return;
    this.camera.set(null);
    this.api.getClockCameraCheck(this.cameraBranch(), this.cameraDay()).subscribe({ next: (c) => this.camera.set(c), error: () => {} });
  }

  load(): void {
    if (!this.from() || !this.to()) return;
    this.api.getHoursReport(this.from(), this.to()).subscribe({
      next: (r) => this.report.set(r),
      error: () => {},
    });
    // Anyone active can be picked for a missed shift.
    this.api.getStaffPinPeople().subscribe({
      next: (l) =>
        this.people.set(l.filter((p) => p.isActive).map((p) => ({ adminUserId: p.id, username: p.username, role: p.role, hours: 0, days: 0, problems: 0 }))),
      error: () => {},
    });
  }

  /** The value a datetime-local input wants, in the browser's own time. */
  private local(iso: string | null): string {
    if (!iso) return '';
    const d = new Date(iso);
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  startEdit(e: TimeEntry): void {
    this.deleting.set(null);
    this.reason = '';
    this.editIn = this.local(e.clockInAt);
    this.editOut = e.forgotten || e.open ? '' : this.local(e.clockOutAt);
    this.editing.set(this.editing()?.id === e.id ? null : e);
  }

  saveEdit(): void {
    const e = this.editing();
    if (!e || !this.reason.trim() || !this.editIn || !this.editOut) return;
    this.busy.set(true);
    this.api
      .changeTimeEntry(e.id, { clockInAt: new Date(this.editIn).toISOString(), clockOutAt: new Date(this.editOut).toISOString(), reason: this.reason.trim() })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.editing.set(null);
          this.toast.success(this.lang.t('hours.saved'));
          this.load();
        },
        error: () => this.busy.set(false),
      });
  }

  startDelete(e: TimeEntry): void {
    this.editing.set(null);
    this.reason = '';
    this.deleting.set(this.deleting()?.id === e.id ? null : e);
  }

  confirmDelete(): void {
    const e = this.deleting();
    if (!e || !this.reason.trim()) return;
    this.busy.set(true);
    this.api.deleteTimeEntry(e.id, this.reason.trim()).subscribe({
      next: () => {
        this.busy.set(false);
        this.deleting.set(null);
        this.toast.success(this.lang.t('hours.removed'));
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }

  add(): void {
    if (!this.addUser || !this.addIn || !this.addOut || !this.addReason.trim()) return;
    this.busy.set(true);
    this.api
      .addTimeEntry({ adminUserId: this.addUser, clockInAt: new Date(this.addIn).toISOString(), clockOutAt: new Date(this.addOut).toISOString(), reason: this.addReason.trim() })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.addIn = '';
          this.addOut = '';
          this.addReason = '';
          this.toast.success(this.lang.t('hours.saved'));
          this.load();
        },
        error: () => this.busy.set(false),
      });
  }
}
