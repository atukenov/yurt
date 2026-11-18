import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeMode, ThemeService } from 'shared-api';
import { Lang, LangService } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';

/** Language and light/dark appearance, opened from the profile. Both are saved on this device. */
@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [RouterLink, TranslatePipe],
  template: `
    <div class="min-h-screen bg-stone-50 pb-8">
      <div class="bg-linear-to-br from-brand-500 to-brand-600 shadow-md px-4 pt-5 pb-4">
        <a routerLink="/profile" class="text-xs text-brand-100 font-semibold">‹ {{ 'nav.profile' | t }}</a>
        <h1 class="text-xl font-bold text-white mt-1">{{ 'profile.settings' | t }}</h1>
      </div>

      <p class="section-label" style="margin:18px 20px 8px">{{ 'profile.language' | t }}</p>
      <div style="margin:0 16px;display:flex;gap:8px" role="group" [attr.aria-label]="'profile.language' | t">
        @for (l of langs; track l.code) {
          <button (click)="langService.setLang(l.code)" [attr.aria-pressed]="langService.lang() === l.code"
            class="active:scale-[0.97] transition-all"
            style="flex:1;padding:11px;border-radius:14px;border:none;font-size:13px;font-weight:700;cursor:pointer;transition:all .2s"
            [style.background]="langService.lang() === l.code ? 'var(--brand-500)' : 'var(--ink-100)'"
            [style.color]="langService.lang() === l.code ? 'var(--brand-on-primary)' : 'var(--ink-600)'">
            {{ l.label }}
          </button>
        }
      </div>

      <p class="section-label" style="margin:18px 20px 8px">{{ 'profile.theme' | t }}</p>
      <div style="margin:0 16px;display:flex;gap:8px" role="group" [attr.aria-label]="'profile.theme' | t">
        @for (m of themeModes; track m) {
          <button (click)="theme.set(m)" [attr.aria-pressed]="theme.mode() === m"
            class="active:scale-[0.97] transition-all"
            style="flex:1;padding:11px;border-radius:14px;border:none;font-size:13px;font-weight:700;cursor:pointer;transition:all .2s"
            [style.background]="theme.mode() === m ? 'var(--brand-500)' : 'var(--ink-100)'"
            [style.color]="theme.mode() === m ? 'var(--brand-on-primary)' : 'var(--ink-600)'">
            {{ 'profile.theme.' + m | t }}
          </button>
        }
      </div>
    </div>
  `,
})
export class SettingsComponent {
  readonly langService = inject(LangService);
  readonly theme = inject(ThemeService);
  readonly themeModes: ThemeMode[] = ['light', 'dark', 'system'];
  readonly langs: { code: Lang; label: string }[] = [
    { code: 'ru', label: 'Русский' },
    { code: 'kk', label: 'Қазақша' },
    { code: 'en', label: 'English' },
  ];
}
