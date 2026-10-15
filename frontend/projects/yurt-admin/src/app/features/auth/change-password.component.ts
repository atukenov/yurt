import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { ButtonComponent } from 'shared-ui';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent],
  template: `
    <div class="min-h-screen bg-gradient-to-br from-stone-800 to-stone-900 flex items-center justify-center p-4">
      <div class="bg-white rounded-3xl shadow-2xl w-full max-w-sm p-8">
        <div class="text-center mb-8">
          <img src="logo.png" alt="Altyncup" class="w-16 h-16 object-contain mx-auto mb-3" />
          <h1 class="text-2xl font-bold text-stone-800">Change Password</h1>
          <p class="text-slate-400 text-sm mt-1">You must set a new password before continuing.</p>
        </div>

        <form (ngSubmit)="submit()" #form="ngForm">
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">Current Password</label>
              <input
                type="password"
                name="currentPassword"
                [(ngModel)]="currentPassword"
                required
                autocomplete="current-password"
                class="w-full border border-slate-200 rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-amber-400 focus:border-transparent outline-none transition-all"
                placeholder="••••••••"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">New Password</label>
              <input
                type="password"
                name="newPassword"
                [(ngModel)]="newPassword"
                required
                minlength="8"
                autocomplete="new-password"
                class="w-full border border-slate-200 rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-amber-400 focus:border-transparent outline-none transition-all"
                placeholder="••••••••"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-slate-700 mb-1.5">Confirm New Password</label>
              <input
                type="password"
                name="confirmPassword"
                [(ngModel)]="confirmPassword"
                required
                autocomplete="new-password"
                class="w-full border border-slate-200 rounded-xl px-4 py-3 text-sm focus:ring-2 focus:ring-amber-400 focus:border-transparent outline-none transition-all"
                placeholder="••••••••"
              />
            </div>
          </div>

          @if (error()) {
            <p class="mt-3 text-red-600 text-xs text-center">{{ error() }}</p>
          }
          @if (success()) {
            <p class="mt-3 text-green-600 text-xs text-center">Password changed! Redirecting...</p>
          }

          <div class="mt-6">
            <yurt-button
              type="submit"
              variant="primary"
              size="lg"
              [fullWidth]="true"
              [loading]="loading()"
            >
              Set New Password
            </yurt-button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class ChangePasswordComponent {
  private api = inject(YurtApiService);
  private router = inject(Router);

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
      this.error.set('New passwords do not match.');
      return;
    }
    if (this.newPassword.length < 8) {
      this.error.set('New password must be at least 8 characters.');
      return;
    }
    this.loading.set(true);
    this.api.adminChangePassword(this.currentPassword, this.newPassword).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set(true);
        setTimeout(() => this.router.navigate(['/dashboard']), 1500);
      },
      error: (err) => {
        this.loading.set(false);
        const detail = err?.error?.detail ?? err?.error?.title;
        this.error.set(detail ?? 'Failed to change password. Please try again.');
      },
    });
  }
}
