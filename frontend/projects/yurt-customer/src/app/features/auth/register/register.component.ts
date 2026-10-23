import { Component, inject, signal, viewChild, viewChildren, ElementRef, OnDestroy } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { YurtApiService, AuthStateService, BrandService } from 'shared-api';
import { ButtonComponent, OtpBoxesComponent, ToastService } from 'shared-ui';
import { LangService } from '../../../core/lang.service';
import { TranslatePipe } from '../../../core/translate.pipe';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, OtpBoxesComponent, TranslatePipe],
  templateUrl: './register.component.html',
  styleUrl: './register.component.css',
})
export class RegisterComponent implements OnDestroy {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);
  readonly brand = inject(BrandService);
  private lang = inject(LangService);

  readonly pinInputs = viewChildren<ElementRef>('pinInput');
  readonly confirmInputs = viewChildren<ElementRef>('confirmInput');
  readonly otpBoxes = viewChild<OtpBoxesComponent>('otpBoxes');

  step = signal<'form' | 'otp'>('form');

  phoneFormatted = '';
  firstName = '';
  lastName = '';
  pins: string[] = ['', '', '', ''];
  confirmPins: string[] = ['', '', '', ''];
  otp = '';
  loading = signal(false);
  error = signal('');
  resendIn = signal(0);

  private pendingPhone = '';
  private resendTimer: ReturnType<typeof setInterval> | null = null;

  ngOnDestroy(): void {
    this.clearResendTimer();
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
  }

  get pin4(): string {
    return this.pins.join('');
  }
  get confirmPin4(): string {
    return this.confirmPins.join('');
  }

  onPinInput(index: number, event: Event): void {
    const val = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(-1);
    this.pins[index] = val;
    if (val && index < 3) this.pinInputs()[index + 1]?.nativeElement.focus();
  }

  onPinKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.pins[index] && index > 0)
      this.pinInputs()[index - 1]?.nativeElement.focus();
  }

  onConfirmInput(index: number, event: Event): void {
    const val = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(-1);
    this.confirmPins[index] = val;
    if (val && index < 3) this.confirmInputs()[index + 1]?.nativeElement.focus();
  }

  onRegister(): void {
    this.error.set('');
    if (!this.firstName.trim()) {
      this.error.set(this.lang.t('auth.errFirstName'));
      return;
    }
    if (!this.lastName.trim()) {
      this.error.set(this.lang.t('auth.errLastName'));
      return;
    }
    if (!/^\d{10}$/.test(this.phoneDigits)) {
      this.error.set(this.lang.t('auth.errPhone'));
      return;
    }
    if (this.pin4.length !== 4) {
      this.error.set(this.lang.t('auth.errPin'));
      return;
    }
    if (this.pin4 !== this.confirmPin4) {
      this.error.set(this.lang.t('auth.errPinMismatch'));
      return;
    }

    const phone = '+7' + this.phoneDigits;
    this.loading.set(true);
    this.api
      .registerStart(phone, this.pin4, this.firstName.trim(), this.lastName.trim())
      .subscribe({
        next: (res) => {
          // Phone verification switched off on the server: the account already exists.
          if (res.verificationRequired === false && res.auth) {
            this.auth.setUser({
              accessToken: res.auth.accessToken,
              refreshToken: res.auth.refreshToken,
              userId: res.auth.userId,
              displayName: res.auth.displayName,
              userType: res.auth.userType,
            });
            this.toast.success(this.lang.t('auth.accountCreated', { name: this.brand.name() }));
            this.router.navigate(['/locations']);
            return;
          }
          this.loading.set(false);
          this.pendingPhone = res.mobileNumber || phone;
          this.otp = '';
          this.step.set('otp');
          this.startResendCountdown();
          if (res.devCode) {
            this.toast.success(this.lang.t('auth.devCode', { code: res.devCode }));
          }
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(err.error?.title ?? this.lang.t('auth.registerFailed'));
        },
      });
  }

  onVerify(): void {
    this.error.set('');
    if (this.otp.length !== 4) {
      this.error.set(this.lang.t('auth.errCode'));
      return;
    }

    this.loading.set(true);
    this.api.registerVerify(this.pendingPhone, this.otp).subscribe({
      next: (res) => {
        this.auth.setUser({
          accessToken: res.accessToken,
          refreshToken: res.refreshToken,
          userId: res.userId,
          displayName: res.displayName,
          userType: res.userType,
        });
        this.toast.success(this.lang.t('auth.accountCreated', { name: this.brand.name() }));
        this.router.navigate(['/locations']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.title ?? this.lang.t('auth.verifyFailed'));
      },
    });
  }

  onResend(): void {
    if (this.resendIn() > 0) return;
    this.error.set('');
    this.loading.set(true);
    this.api
      .registerStart(this.pendingPhone, this.pin4, this.firstName.trim(), this.lastName.trim())
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          this.otpBoxes()?.clear();
          this.startResendCountdown();
          if (res.devCode) this.toast.success(this.lang.t('auth.devCode', { code: res.devCode }));
          else this.toast.success(this.lang.t('auth.codeSent'));
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(err.error?.title ?? this.lang.t('auth.resendFailed'));
        },
      });
  }

  onBack(): void {
    this.error.set('');
    this.clearResendTimer();
    this.resendIn.set(0);
    this.step.set('form');
  }

  private startResendCountdown(): void {
    this.clearResendTimer();
    this.resendIn.set(60);
    this.resendTimer = setInterval(() => {
      const next = this.resendIn() - 1;
      this.resendIn.set(next);
      if (next <= 0) this.clearResendTimer();
    }, 1000);
  }

  private clearResendTimer(): void {
    if (this.resendTimer) {
      clearInterval(this.resendTimer);
      this.resendTimer = null;
    }
  }
}
