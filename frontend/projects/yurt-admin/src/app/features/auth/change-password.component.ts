import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthStateService, BrandService, YurtApiService } from 'shared-api';
import { ButtonComponent } from 'shared-ui';
import { AdminLangService } from '../../core/lang.service';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { homeLink } from '../../layout/admin-nav';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent, AdminTranslatePipe],
  template: `
    <div class="min-h-screen bg-gradient-to-br from-night-800 to-night-900 flex items-center justify-center p-4">
      <div class="bg-surface rounded-3xl shadow-2xl w-full max-w-sm p-8">
        <div class="text-center mb-8">
          <img [src]="brand.logo() ?? 'logo.png'" [alt]="brand.name()" class="w-16 h-16 object-contain mx-auto mb-3" />
          <h1 class="text-2xl font-bold text-stone-800">{{ 'auth.changeTitle' | t }}</h1>
          <p class="text-slate-500 text-sm mt-1">{{ 'auth.changeHint' | t }}</p>
        </div>

        <form (ngSubmit)="submit()" #form="ngForm">
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">{{ 'auth.currentPw' | t }}</label>
              <input
                type="password"
                name="currentPassword"
                [(ngModel)]="currentPassword"
                required
                autocomplete="current-password"
                class="w-full border border-slate-200 rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-brand-400 focus:border-transparent outline-none transition-all"
                placeholder="••••••••"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">{{ 'auth.newPw' | t }}</label>
              <input
                type="password"
                name="newPassword"
                [(ngModel)]="newPassword"
                required
                minlength="8"
                autocomplete="new-password"
                class="w-full border border-slate-200 rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-brand-400 focus:border-transparent outline-none transition-all"
                placeholder="••••••••"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">{{ 'auth.repeatPw' | t }}</label>
              <input
                type="password"
                name="confirmPassword"
                [(ngModel)]="confirmPassword"
                required
                autocomplete="new-password"
                class="w-full border border-slate-200 rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-brand-400 focus:border-transparent outline-none transition-all"
                placeholder="••••••••"
              />
            </div>
          </div>

          @if (error()) {
            <p class="mt-3 text-tone-red text-xs text-center">{{ error() }}</p>
          }
          @if (success()) {
            <p class="mt-3 text-green-600 text-xs text-center">{{ 'auth.changed' | t }}</p>
          }

          <div class="mt-6">
            <yurt-button
              type="submit"
              variant="primary"
              size="lg"
              [fullWidth]="true"
              [loading]="loading()"
            >
              {{ 'auth.setNew' | t }}
            </yurt-button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class ChangePasswordComponent {
  readonly brand = inject(BrandService);
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private lang = inject(AdminLangService);

  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  loading = signal(false);
  error = signal('');
  success = signal(false);

  submit(): void {
    this.error.set('');
    if (!this.currentPassword || !this.newPassword || !this.confirmPassword) return;
    if (this.newPassword !== this.confirmPassword) {
      this.error.set(this.lang.t('auth.noMatch'));
      return;
    }
    if (this.newPassword.length < 8) {
      this.error.set(this.lang.t('auth.tooShort'));
      return;
    }
    this.loading.set(true);
    this.api.adminChangePassword(this.currentPassword, this.newPassword).subscribe({
      next: (res) => {
        // The old token still says "must change password"; swap in the fresh one.
        this.auth.updateTokens(res.accessToken, res.refreshToken);
        this.loading.set(false);
        this.success.set(true);
        setTimeout(() => this.router.navigateByUrl(homeLink(this.auth.isSuperAdmin)), 1500);
      },
      error: (err) => {
        this.loading.set(false);
        const detail = err?.error?.detail ?? err?.error?.title;
        this.error.set(detail ?? this.lang.t('auth.changeFailed'));
      },
    });
  }
}
