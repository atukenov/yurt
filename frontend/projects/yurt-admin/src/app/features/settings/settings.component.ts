import { Component, inject } from '@angular/core';
import { ThemeMode, ThemeService } from 'shared-api';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';

/** Language and light/dark appearance, kept out of the sidebar. Both are saved on this device. */
@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [AdminTranslatePipe],
  template: `
    <div class="p-4 md:p-6 max-w-2xl mx-auto space-y-5 overflow-y-auto">
      <div>
        <h1 class="text-2xl font-bold text-slate-800">{{ 'settings.title' | t }}</h1>
        <p class="text-slate-500 text-sm mt-0.5">{{ 'settings.hint' | t }}</p>
      </div>

      <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
        <h2 class="font-semibold text-slate-800 mb-3">{{ 'settings.language' | t }}</h2>
        <div class="flex gap-2" role="group" [attr.aria-label]="'settings.language' | t">
          @for (l of langs; track l.code) {
            <button type="button" (click)="langService.setLang(l.code)" [attr.aria-pressed]="langService.lang() === l.code"
              [class]="langService.lang() === l.code
                ? 'flex-1 py-2 text-sm font-semibold rounded-xl bg-brand-50 text-brand-ink'
                : 'flex-1 py-2 text-sm rounded-xl text-slate-600 bg-slate-50 hover:bg-slate-100 transition-colors'">
              {{ l.label }}
            </button>
          }
        </div>
      </section>

      <section class="bg-surface rounded-2xl border border-slate-100 shadow-sm p-4 md:p-5">
        <h2 class="font-semibold text-slate-800 mb-3">{{ 'theme.label' | t }}</h2>
        <div class="flex gap-2" role="group" [attr.aria-label]="'theme.label' | t">
          @for (m of themeModes; track m) {
            <button type="button" (click)="theme.set(m)" [attr.aria-pressed]="theme.mode() === m"
              [class]="theme.mode() === m
                ? 'flex-1 py-2 text-sm font-semibold rounded-xl bg-brand-50 text-brand-ink'
                : 'flex-1 py-2 text-sm rounded-xl text-slate-600 bg-slate-50 hover:bg-slate-100 transition-colors'">
              {{ 'theme.' + m | t }}
            </button>
          }
        </div>
      </section>
    </div>
  `,
})
export class SettingsComponent {
  readonly langService = inject(AdminLangService);
  readonly theme = inject(ThemeService);
  readonly themeModes: ThemeMode[] = ['light', 'dark', 'system'];
  readonly langs = [
    { code: 'ru', label: 'Русский' },
    { code: 'en', label: 'English' },
    { code: 'kk', label: 'Қазақша' },
  ];
}
