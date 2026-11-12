import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthStateService, BrandService, YurtApiService } from 'shared-api';
import { PosLang, PosLangService } from '../../core/lang.service';
import { PosTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'pos-login',
  imports: [FormsModule, PosTranslatePipe],
  template: `
    <main class="min-h-dvh flex items-center justify-center p-4">
      <form (ngSubmit)="login()" class="w-full max-w-sm rounded-2xl bg-surface p-6 shadow-sm ring-1 ring-slate-200 space-y-4">
        <div class="flex items-center gap-3">
          <img [src]="brand.logo() || 'logo.png'" alt="" class="brand-logo h-10 w-10 object-contain" />
          <div>
            <p class="text-lg font-bold text-slate-900">{{ 'app.name' | t: { cafe: brand.name() } }}</p>
            <h1 class="text-sm text-slate-600">{{ 'login.title' | t }}</h1>
          </div>
        </div>
        <label class="block">
          <span class="text-sm font-medium text-slate-700">{{ 'login.username' | t }}</span>
          <input name="username" [(ngModel)]="username" autocomplete="username" autocapitalize="off"
                 class="mt-1 w-full rounded-xl border border-slate-300 px-3 py-3 text-base" />
        </label>
        <label class="block">
          <span class="text-sm font-medium text-slate-700">{{ 'login.password' | t }}</span>
          <input name="password" type="password" [(ngModel)]="password" autocomplete="current-password"
                 class="mt-1 w-full rounded-xl border border-slate-300 px-3 py-3 text-base" />
        </label>
        @if (error()) {
          <p class="text-sm text-red-700" role="alert">{{ error() | t }}</p>
        }
        <button type="submit" [disabled]="loading() || !username || !password"
                class="w-full rounded-xl bg-brand-500 px-4 py-3 text-base font-semibold text-on-brand disabled:opacity-50">
          {{ 'login.submit' | t }}
        </button>
        <p class="text-xs text-slate-500">{{ 'login.hint' | t }}</p>
        <div class="flex justify-center gap-2 pt-1">
          @for (l of langs; track l.code) {
            <button type="button" (click)="lang.setLang(l.code)"
                    [class]="'rounded-lg px-3 py-1.5 text-sm ' + (lang.lang() === l.code ? 'bg-slate-900 text-white' : 'text-slate-600')">
              {{ l.label }}
            </button>
          }
        </div>
      </form>
    </main>
  `,
})
export class LoginComponent {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  readonly brand = inject(BrandService);
  readonly lang = inject(PosLangService);
  readonly langs: { code: PosLang; label: string }[] = [
    { code: 'ru', label: 'Русский' }, { code: 'kk', label: 'Қазақша' }, { code: 'en', label: 'English' },
  ];

  username = '';
  password = '';
  readonly loading = signal(false);
  /** A translation key. */
  readonly error = signal('');

  login(): void {
    if (!this.username || !this.password) return;
    this.loading.set(true);
    this.error.set('');
    this.api.adminLogin(this.username.trim(), this.password).subscribe({
      next: (res) => {
        this.loading.set(false);
        // A new password is set in the admin app, which has that screen.
        if (res.mustChangePassword || res.role === 'SuperAdmin') {
          this.error.set(res.mustChangePassword ? 'login.changePassword' : 'login.invalid');
          return;
        }
        this.auth.setUser({
          accessToken: res.accessToken, refreshToken: res.refreshToken, userId: res.userId,
          displayName: res.displayName, userType: 'admin', role: res.role, cafe: res.cafe ?? null,
        });
        this.router.navigateByUrl('/');
      },
      error: () => {
        this.loading.set(false);
        this.error.set('login.invalid');
      },
    });
  }
}
