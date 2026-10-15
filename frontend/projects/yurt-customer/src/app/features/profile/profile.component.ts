import { Component, inject, OnDestroy, OnInit, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { YurtApiService, AuthStateService, NotificationService } from 'shared-api';
import { CustomerProfile, CustomerStats, LoyaltyBalance } from 'shared-models';
import { Currency2Pipe, OtpBoxesComponent, ToastService } from 'shared-ui';
import { LangService, Lang } from '../../core/lang.service';
import { TranslatePipe } from '../../core/translate.pipe';
import { PullToRefreshDirective } from '../../shared/pull-to-refresh.directive';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    Currency2Pipe,
    OtpBoxesComponent,
    TranslatePipe,
    PullToRefreshDirective,
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.css',
})
export class ProfileComponent implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private notifications = inject(NotificationService);
  readonly langService = inject(LangService);
  readonly langs: { code: Lang; label: string }[] = [
    { code: 'en', label: 'EN' },
    { code: 'ru', label: 'RU' },
    { code: 'kk', label: 'KZ' },
  ];

  profile = signal<CustomerProfile | null>(null);
  stats = signal<CustomerStats | null>(null);
  loyalty = signal<LoyaltyBalance | null>(null);
  editMode = signal(false);
  saving = signal(false);
  editFirstName = '';
  editLastName = '';
  editDateOfBirth = '';
  readonly maxDateOfBirth = new Date().toISOString().slice(0, 10);

  // Change PIN
  showPinForm = signal(false);
  currentPin = '';
  newPin = '';
  pinLoading = signal(false);

  // Change phone number (two-step: request code, then verify it)
  showPhoneForm = signal(false);
  phoneStep = signal<'form' | 'otp'>('form');
  phoneFormatted = '';
  phoneOtp = '';
  phoneLoading = signal(false);
  phoneError = signal('');
  phoneResendIn = signal(0);
  readonly phoneOtpBoxes = viewChild<OtpBoxesComponent>('phoneOtpBoxes');
  private pendingPhoneNumber = '';
  private phoneResendTimer: ReturnType<typeof setInterval> | null = null;

  // Delete account
  showDeleteConfirm = signal(false);
  deleteLoading = signal(false);

  // Notifications
  notificationsEnabled = signal(localStorage.getItem('yurt_push_enabled') !== 'false');

  // Report a Problem
  showReportModal = signal(false);
  reportText = '';
  reportLoading = signal(false);

  // Avatar picker — stored locally until there's a backend field for it
  private static readonly AVATAR_KEY = 'yurt_avatar';
  readonly avatars = Array.from({ length: 24 }, (_, i) => `/avatars/avatar-${String(i + 1).padStart(2, '0')}.png`);
  showAvatarPicker = signal(false);
  selectedAvatar = signal<string | null>(localStorage.getItem(ProfileComponent.AVATAR_KEY));

  ngOnInit(): void {
    this.loadProfile();
  }

  ngOnDestroy(): void {
    this.clearPhoneResendTimer();
  }

  chooseAvatar(avatar: string): void {
    this.selectedAvatar.set(avatar);
    localStorage.setItem(ProfileComponent.AVATAR_KEY, avatar);
    this.showAvatarPicker.set(false);
  }

  loadProfile(): void {
    this.api.me().subscribe({ next: (p) => this.profile.set(p), error: () => {} });
    this.api.getCustomerStats().subscribe({ next: (s) => this.stats.set(s), error: () => {} });
    this.api.getLoyaltyBalance().subscribe({ next: (l) => this.loyalty.set(l), error: () => {} });
  }

  async toggleNotifications(): Promise<void> {
    const current = this.notificationsEnabled();
    if (!current) {
      await this.notifications.requestWebNotificationPermission();
    }
    this.notificationsEnabled.set(!current);
    localStorage.setItem('yurt_push_enabled', (!current).toString());
  }

  submitPinChange(): void {
    if (!this.currentPin || !this.newPin || this.currentPin.length !== 4 || this.newPin.length !== 4) {
      this.toast.error('Both PINs must be exactly 4 digits.');
      return;
    }
    this.pinLoading.set(true);
    this.api.changePin(this.currentPin, this.newPin).subscribe({
      next: () => {
        this.showPinForm.set(false);
        this.currentPin = '';
        this.newPin = '';
        this.pinLoading.set(false);
        this.toast.success('PIN changed successfully.');
      },
      error: (err) => {
        this.pinLoading.set(false);
        this.toast.error(err.status === 401 ? 'Current PIN is incorrect.' : 'Failed to change PIN.');
      },
    });
  }

  get phoneDigits(): string {
    return this.phoneFormatted.replace(/\D/g, '').slice(-10);
  }

  private formatPhone(raw: string): string {
    let digits = raw.replace(/\D/g, '');
    if (raw.trimStart().startsWith('+')) {
      digits = digits.slice(1);
    } else if (digits.length > 10) {
      digits = digits.slice(1);
    }
    const s = digits.slice(0, 10);
    if (!s.length) return '';
    if (s.length <= 3) return `+7 (${s}`;
    if (s.length <= 6) return `+7 (${s.slice(0, 3)}) ${s.slice(3)}`;
    return `+7 (${s.slice(0, 3)}) ${s.slice(3, 6)}-${s.slice(6)}`;
  }

  onPhoneInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const formatted = this.formatPhone(input.value);
    this.phoneFormatted = formatted;
    input.value = formatted;
    this.phoneError.set('');
  }

  toggleShowPhoneForm(): void {
    this.showPhoneForm.set(!this.showPhoneForm());
    this.phoneError.set('');
    this.phoneStep.set('form');
    this.clearPhoneResendTimer();
    this.phoneResendIn.set(0);
  }

  submitPhoneChange(): void {
    this.phoneError.set('');
    if (!/^\d{10}$/.test(this.phoneDigits)) {
      this.phoneError.set(this.langService.t('profile.invalidPhone'));
      return;
    }
    const newNumber = '+7' + this.phoneDigits;
    this.phoneLoading.set(true);
    this.api.changeMobileNumberStart(newNumber).subscribe({
      next: (res) => {
        this.phoneLoading.set(false);
        this.pendingPhoneNumber = res.mobileNumber || newNumber;
        this.phoneOtp = '';
        this.phoneStep.set('otp');
        this.startPhoneResendCountdown();
        if (res.devCode) this.toast.success('Dev code: ' + res.devCode);
      },
      error: (err) => {
        this.phoneLoading.set(false);
        this.phoneError.set(
          err.status === 409 ? this.langService.t('profile.phoneInUse') : 'Could not send the verification code.',
        );
      },
    });
  }

  verifyPhoneChange(): void {
    this.phoneError.set('');
    if (this.phoneOtp.length !== 4) {
      this.phoneError.set('Enter the 4-digit code.');
      return;
    }
    this.phoneLoading.set(true);
    this.api.changeMobileNumberVerify(this.pendingPhoneNumber, this.phoneOtp).subscribe({
      next: (p) => {
        this.profile.set(p);
        this.clearPhoneResendTimer();
        this.showPhoneForm.set(false);
        this.phoneStep.set('form');
        this.phoneFormatted = '';
        this.phoneLoading.set(false);
        this.toast.success(this.langService.t('profile.phoneChanged'));
      },
      error: (err) => {
        this.phoneLoading.set(false);
        this.phoneError.set(
          err.status === 409 ? this.langService.t('profile.phoneInUse') : (err.error?.title ?? 'Incorrect code.'),
        );
      },
    });
  }

  resendPhoneChangeCode(): void {
    if (this.phoneResendIn() > 0) return;
    this.phoneError.set('');
    this.phoneLoading.set(true);
    this.api.changeMobileNumberStart(this.pendingPhoneNumber).subscribe({
      next: (res) => {
        this.phoneLoading.set(false);
        this.phoneOtpBoxes()?.clear();
        this.startPhoneResendCountdown();
        this.toast.success(res.devCode ? 'Dev code: ' + res.devCode : 'Code sent.');
      },
      error: (err) => {
        this.phoneLoading.set(false);
        this.phoneError.set(err.error?.title ?? 'Could not resend the code.');
      },
    });
  }

  backToPhoneForm(): void {
    this.phoneError.set('');
    this.clearPhoneResendTimer();
    this.phoneResendIn.set(0);
    this.phoneStep.set('form');
  }

  private startPhoneResendCountdown(): void {
    this.clearPhoneResendTimer();
    this.phoneResendIn.set(60);
    this.phoneResendTimer = setInterval(() => {
      const next = this.phoneResendIn() - 1;
      this.phoneResendIn.set(next);
      if (next <= 0) this.clearPhoneResendTimer();
    }, 1000);
  }

  private clearPhoneResendTimer(): void {
    if (this.phoneResendTimer) {
      clearInterval(this.phoneResendTimer);
      this.phoneResendTimer = null;
    }
  }

  confirmDelete(): void {
    this.deleteLoading.set(true);
    this.api.deleteAccount().subscribe({
      next: () => {
        this.auth.logout();
        this.toast.info('Account deleted.');
        this.router.navigate(['/auth/login']);
      },
      error: () => this.deleteLoading.set(false),
    });
  }

  startEdit(): void {
    const p = this.profile();
    this.editFirstName = p?.firstName ?? '';
    this.editLastName = p?.lastName ?? '';
    this.editDateOfBirth = p?.dateOfBirth ?? '';
    this.editMode.set(true);
  }

  saveProfile(): void {
    if (!this.editFirstName.trim() || !this.editLastName.trim()) {
      this.toast.error('First and last name are required.');
      return;
    }
    if (this.editDateOfBirth && this.editDateOfBirth > this.maxDateOfBirth) {
      this.toast.error(this.langService.t('profile.invalidBirthday'));
      return;
    }
    this.saving.set(true);
    this.api
      .updateProfile({
        firstName: this.editFirstName.trim(),
        lastName: this.editLastName.trim(),
        dateOfBirth: this.editDateOfBirth || null,
      })
      .subscribe({
        next: (p) => {
          this.profile.set(p);
          this.editMode.set(false);
          this.saving.set(false);
          this.toast.success('Profile updated.');
        },
        error: () => this.saving.set(false),
      });
  }

  get initials(): string {
    const p = this.profile();
    if (!p) return '';
    if (p.firstName && p.lastName) return (p.firstName[0] + p.lastName[0]).toUpperCase();
    if (p.firstName) return p.firstName[0].toUpperCase();
    return p.mobileNumber.slice(0, 2).toUpperCase();
  }

  logout(): void {
    this.auth.logout();
    this.toast.info('Signed out');
    this.router.navigate(['/auth/login']);
  }

  submitReport(): void {
    const text = this.reportText.trim();
    if (!text) return;
    this.reportLoading.set(true);
    this.api.submitReport(text).subscribe({
      next: () => {
        this.reportLoading.set(false);
        this.reportText = '';
        this.showReportModal.set(false);
        this.toast.success(this.langService.t('profile.reportSuccess'));
      },
      error: () => this.reportLoading.set(false),
    });
  }
}
