import { Component, inject, input, output, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { YurtApiService } from 'shared-api';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { AdminLangService } from '../../core/lang.service';
import { shrinkPicture } from './shrink-picture';

/**
 * "Upload picture" button that sits next to a picture link field (logo, icon, splash, menu or promotion photo).
 * It shrinks big photos in the browser, uploads the file to the cafe's own picture storage and hands back the
 * public link, which the screen puts in the field. Typing a link by hand keeps working.
 */
@Component({
  selector: 'app-picture-upload',
  standalone: true,
  imports: [AdminTranslatePipe],
  template: `
    <div class="flex items-center gap-2 mt-1.5">
      <label
        class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg border border-slate-200 text-xs font-medium text-slate-600 hover:bg-slate-50 cursor-pointer focus-within:ring-2 focus-within:ring-brand-400"
        [class.opacity-60]="busy()"
        [class.pointer-events-none]="busy()"
      >
        <input type="file" class="sr-only" accept="image/png,image/jpeg,image/webp" [disabled]="busy()"
          [attr.aria-label]="label() || ('pic.upload' | t)" (change)="pick($event)" />
        {{ busy() ? ('pic.uploading' | t) : ('pic.upload' | t) }}
      </label>
      <span class="text-xs text-slate-500">{{ 'pic.hint' | t }}</span>
    </div>
    @if (error()) {
      <p class="text-xs text-tone-red mt-1" role="alert">{{ error() }}</p>
    }
  `,
})
export class PictureUploadComponent {
  private api = inject(YurtApiService);
  private lang = inject(AdminLangService);

  /** Accessible name of the file picker, e.g. the field's label. */
  label = input<string>('');
  /** Longest side in pixels after shrinking: smaller for icons and logos, bigger for photos. */
  maxSide = input(1600);
  /** The Brand screen: upload into the cafe the operator is managing. */
  forManagedCafe = input(false);

  /** The picture's full public link. */
  uploaded = output<string>();

  busy = signal(false);
  error = signal('');

  async pick(event: Event): Promise<void> {
    const inputEl = event.target as HTMLInputElement;
    const file = inputEl.files?.[0];
    inputEl.value = ''; // picking the same file again still fires
    if (!file) return;
    this.error.set('');
    if (!/^image\/(png|jpeg|webp)$/.test(file.type)) {
      this.error.set(this.lang.t('pic.wrongType'));
      return;
    }
    this.busy.set(true);
    let blob: Blob;
    try {
      blob = await shrinkPicture(file, this.maxSide());
    } catch {
      blob = file; // could not read it in the browser: let the server decide
    }
    this.api.uploadPicture(blob, this.forManagedCafe()).subscribe({
      next: (url) => {
        this.busy.set(false);
        this.uploaded.emit(url);
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        const key =
          err.status === 413 ? 'pic.tooBig'
          : err.status === 415 ? 'pic.wrongType'
          : err.status === 409 ? 'pic.full'
          : 'pic.failed';
        this.error.set(this.lang.t(key));
      },
    });
  }
}
