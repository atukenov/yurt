import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { ToastService } from 'shared-ui';
import { IntegrationStatus, PlatformModule } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

interface ModuleEdit {
  enabled: boolean;
  validUntil: string; // yyyy-MM-dd or ''
  source: 'Manual' | 'Trial' | 'Paid';
  notes: string;
}

@Component({
  selector: 'app-platform',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, AdminTranslatePipe],
  templateUrl: './platform.component.html',
})
export class PlatformComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  modules = signal<PlatformModule[]>([]);
  integrations = signal<IntegrationStatus[]>([]);
  loading = signal(true);
  savingKey = signal<string | null>(null);
  edits: Record<string, ModuleEdit> = {};

  maintenanceEnabled = false;
  maintenanceMessage = '';
  savingMaintenance = signal(false);

  readonly sources: ModuleEdit['source'][] = ['Manual', 'Trial', 'Paid'];

  ngOnInit(): void {
    this.api.getPlatformModules().subscribe({
      next: (m) => { this.setModules(m); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
    this.api.getMaintenance().subscribe({
      next: (m) => { this.maintenanceEnabled = m.enabled; this.maintenanceMessage = m.message; },
    });
    this.api.getIntegrationStatus().subscribe({ next: (i) => this.integrations.set(i) });
  }

  private setModules(list: PlatformModule[]): void {
    this.modules.set(list);
    for (const m of list) this.edits[m.key] = this.toEdit(m);
  }

  private toEdit(m: PlatformModule): ModuleEdit {
    return {
      enabled: m.enabled || m.state === 'grace',
      validUntil: m.validUntil ? m.validUntil.slice(0, 10) : '',
      source: m.source === 'Trial' || m.source === 'Paid' ? m.source : 'Manual',
      notes: m.notes ?? '',
    };
  }

  isDirty(m: PlatformModule): boolean {
    const e = this.edits[m.key];
    const o = this.toEdit(m);
    return !!e && (e.enabled !== o.enabled || e.validUntil !== o.validUntil || e.source !== o.source || e.notes !== o.notes);
  }

  saveModule(m: PlatformModule): void {
    const e = this.edits[m.key];
    this.savingKey.set(m.key);
    this.api.updatePlatformModule(m.key, {
      enabled: e.enabled,
      validUntil: e.validUntil ? `${e.validUntil}T23:59:59Z` : null,
      source: e.source,
      notes: e.notes.trim() || null,
    }).subscribe({
      next: () => {
        // Re-read the whole list so dependency warnings for other modules refresh too.
        this.api.getPlatformModules().subscribe((list) => this.setModules(list));
        this.savingKey.set(null);
        this.toast.success('Saved');
      },
      error: () => this.savingKey.set(null),
    });
  }

  saveMaintenance(): void {
    this.savingMaintenance.set(true);
    this.api.setMaintenance({ enabled: this.maintenanceEnabled, message: this.maintenanceMessage.trim() }).subscribe({
      next: (m) => {
        this.maintenanceEnabled = m.enabled;
        this.maintenanceMessage = m.message;
        this.savingMaintenance.set(false);
        this.toast.success(m.enabled ? 'Maintenance mode is ON' : 'Maintenance mode is off');
      },
      error: () => this.savingMaintenance.set(false),
    });
  }

  badgeClass(m: PlatformModule): string {
    switch (m.state) {
      case 'active': return 'bg-emerald-100 text-emerald-700';
      case 'grace': return 'bg-brand-100 text-brand-700';
      default: return 'bg-slate-100 text-slate-500';
    }
  }
}
