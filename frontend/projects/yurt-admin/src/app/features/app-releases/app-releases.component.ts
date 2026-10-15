import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { ToastService } from 'shared-ui';
import { AppRelease } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

type NotesTab = 'En' | 'Ru' | 'Kk';

@Component({
  selector: 'app-app-releases',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe],
  templateUrl: './app-releases.component.html',
})
export class AppReleasesComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private confirmSvc = inject(ConfirmService);
  private lang = inject(AdminLangService);

  releases = signal<AppRelease[]>([]);
  loading = signal(true);
  showForm = signal(false);
  saving = signal(false);
  editId = signal<string | null>(null);
  tab = signal<NotesTab>('En');
  readonly tabs: NotesTab[] = ['En', 'Ru', 'Kk'];

  form = { version: '', notesEn: '', notesRu: '', notesKk: '', isMandatory: false };

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getAdminAppReleases().subscribe({
      next: (r) => { this.releases.set(r); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  notes(r: AppRelease): string[] {
    return r.notesEn.split('\n').map((l) => l.trim()).filter(Boolean);
  }

  openCreate(): void {
    this.editId.set(null);
    this.form = { version: '', notesEn: '', notesRu: '', notesKk: '', isMandatory: false };
    this.tab.set('En');
    this.showForm.set(true);
  }

  openEdit(r: AppRelease): void {
    this.editId.set(r.id);
    this.form = { version: r.version, notesEn: r.notesEn, notesRu: r.notesRu, notesKk: r.notesKk, isMandatory: r.isMandatory };
    this.tab.set('En');
    this.showForm.set(true);
  }

  get currentNotes(): string {
    return this.form[`notes${this.tab()}` as 'notesEn' | 'notesRu' | 'notesKk'];
  }
  set currentNotes(v: string) {
    this.form[`notes${this.tab()}` as 'notesEn' | 'notesRu' | 'notesKk'] = v;
  }

  save(): void {
    if (!/^\d+(\.\d+){0,3}$/.test(this.form.version.trim())) {
      this.toast.error('Version must look like 5.3.0');
      return;
    }
    if (!this.form.notesEn.trim()) {
      this.toast.error('English notes are required');
      this.tab.set('En');
      return;
    }
    this.saving.set(true);
    const id = this.editId();
    const obs = id ? this.api.updateAppRelease(id, this.form) : this.api.createAppRelease(this.form);
    obs.subscribe({
      next: () => { this.saving.set(false); this.showForm.set(false); this.load(); },
      error: () => this.saving.set(false),
    });
  }

  async remove(r: AppRelease): Promise<void> {
    if (!await this.confirmSvc.confirm(this.lang.t('rel.deleteConfirm'), `v${r.version}`)) return;
    this.api.deleteAppRelease(r.id).subscribe({ next: () => this.load(), error: () => {} });
  }
}
