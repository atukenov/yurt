import { Component, inject, signal, ElementRef, viewChildren } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { YurtApiService, AuthStateService } from 'shared-api';
import { ButtonComponent, ToastService } from 'shared-ui';
import { TranslatePipe } from '../../../core/translate.pipe';
import { LangService } from '../../../core/lang.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ButtonComponent, TranslatePipe],
  templateUrl: './login.component.html',
  styleUrl: './login.component.css',
})
export class LoginComponent {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);
  private lang = inject(LangService);

  readonly pinInputs = viewChildren<ElementRef>('pinInput');

  phoneFormatted = '';
  pins: string[] = ['', '', '', ''];
  loading = signal(false);
  error = signal('');

  get phoneDigits(): string {
    return this.phoneFormatted.replace(/\D/g, '').slice(-10);
  }

  private formatPhone(raw: string): string {
    let digits = raw.replace(/\D/g, '');
    // If the input already has the +7 prefix, strip the leading country-code digit
    if (raw.trimStart().startsWith('+')) {
      digits = digits.slice(1);
    } else if (digits.length > 10) {
      // User pasted/typed a full number with country code prefix
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

  onPinInput(index: number, event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.pins[index] = val.replace(/\D/g, '').slice(-1);
    if (this.pins[index] && index < 3) {
      this.pinInputs()[index + 1]?.nativeElement.focus();
    }
    if (index === 3 && this.pins[3]) {
      this.onLogin();
    }
  }

  onPinKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.pins[index] && index > 0) {
      this.pinInputs()[index - 1]?.nativeElement.focus();
    }
    if (event.key === 'Enter') this.onLogin();
  }

  onPaste(event: ClipboardEvent): void {
    const pasted = event.clipboardData?.getData('text')?.replace(/\D/g, '').slice(0, 4) ?? '';
    pasted.split('').forEach((char, i) => {
      this.pins[i] = char;
    });
    this.pinInputs()[Math.min(pasted.length, 3)]?.nativeElement.focus();
    event.preventDefault();
    if (pasted.length === 4) {
      this.onLogin();
    }
  }

  onLogin(): void {
    this.error.set('');
    if (!/^\d{10}$/.test(this.phoneDigits)) {
      this.error.set('Enter your 10-digit mobile number.');
      return;
    }
    if (this.pin4.length !== 4) {
      this.error.set('Enter your 4-digit PIN.');
      return;
    }

    this.loading.set(true);
    this.api.login('+7' + this.phoneDigits, this.pin4).subscribe({
      next: (res) => {
        this.auth.setUser({
          accessToken: res.accessToken,
          refreshToken: res.refreshToken,
          userId: res.userId,
          displayName: res.displayName,
          userType: res.userType,
        });
        this.router.navigate(['/locations']);
      },
      error: (err) => {
        this.loading.set(false);
        let msg: string;
        if (err.status === 423) {
          const minutes = err.error?.extensions?.minutesRemaining ?? err.error?.minutesRemaining ?? '?';
          msg = this.lang.t('auth.locked').replace('{minutes}', String(minutes));
        } else {
          msg = err.error?.title ?? err.error?.detail ?? 'Login failed. Please try again.';
        }
        this.error.set(msg);
        this.pins = ['', '', '', ''];
        this.pinInputs()[0]?.nativeElement.focus();
      },
    });
  }
}