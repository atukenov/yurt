import { Component, EventEmitter, inject, Input, Output, signal } from '@angular/core';
import { YurtApiService } from 'shared-api';
import { MenuImportError, MenuImportResult } from 'shared-models';
import { ToastService, Currency2Pipe } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/**
 * Fills the menu from an Excel or CSV file: download the template, choose the filled file, see what would be added
 * and every problem by row, then add it. Used on the Menu screen and in the new-cafe wizard.
 */
@Component({
  selector: 'app-menu-import',
  standalone: true,
  imports: [AdminTranslatePipe, Currency2Pipe],
  templateUrl: './menu-import.component.html',
})
export class MenuImportComponent {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private lang = inject(AdminLangService);

  /** Key of the cafe to fill (operator in the new-cafe wizard); empty means this cafe. */
  @Input() cafe?: string;
  @Output() imported = new EventEmitter<MenuImportResult>();

  static readonly MAX_BYTES = 5 * 1024 * 1024;
  /** How many dishes the preview lists before "and N more". */
  readonly previewLimit = 30;

  file = signal<File | null>(null);
  result = signal<MenuImportResult | null>(null);
  busy = signal(false);

  downloadTemplate(): void {
    const lang = this.lang.lang();
    this.api.getMenuTemplate(lang).subscribe((blob) => {
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = `menu-template-${lang}.xlsx`;
      a.click();
      setTimeout(() => URL.revokeObjectURL(a.href), 1000);
    });
  }

  pick(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    this.result.set(null);
    this.file.set(file);
    if (!file) return;
    if (file.size > MenuImportComponent.MAX_BYTES) {
      this.toast.error(this.lang.t('imp.tooBig'));
      this.file.set(null);
      return;
    }
    this.send(false);
  }

  apply(): void {
    this.send(true);
  }

  private send(apply: boolean): void {
    const file = this.file();
    if (!file) return;
    this.busy.set(true);
    this.api.importMenu(file, apply, this.cafe || undefined).subscribe({
      next: (r) => {
        this.result.set(r);
        this.busy.set(false);
        if (r.applied) {
          this.toast.success(this.lang.t('imp.done'));
          this.imported.emit(r);
        }
      },
      error: () => this.busy.set(false),
    });
  }

  reset(): void {
    this.file.set(null);
    this.result.set(null);
  }

  errorText(e: MenuImportError): string {
    const value =
      e.code === 'missingColumns' || e.code === 'tooLong'
        ? (e.value ?? '').split(',').map((c) => this.lang.t(`imp.col.${c}`)).join(', ')
        : (e.value ?? '');
    return this.lang.t(`imp.err.${e.code}`, { value });
  }
}
