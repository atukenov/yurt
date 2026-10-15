import { Component, ElementRef, EventEmitter, Input, OnInit, Output, viewChildren } from '@angular/core';
import { CommonModule } from '@angular/common';

/** A row of single-digit boxes for entering a numeric verification code — focus advances
 * on input and returns to the previous box on backspace. Used for both the mobile-number
 * OTP step and the 4-digit PIN entry that share this exact interaction pattern. */
@Component({
  selector: 'yurt-otp-boxes',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="flex gap-3 justify-center">
      @for (i of indices; track i) {
        <input
          #box
          type="text"
          inputmode="numeric"
          maxlength="1"
          [value]="digits[i]"
          (input)="onInput(i, $event)"
          (keydown)="onKeydown(i, $event)"
          class="pin-box"
          [attr.aria-label]="'Code digit ' + (i + 1)"
        />
      }
    </div>
  `,
  styles: [
    `
      .pin-box {
        width: 3.5rem;
        height: 3.5rem;
        text-align: center;
        font-size: 1.5rem;
        font-weight: 700;
        border: 2px solid #e7e5e4;
        border-radius: 1rem;
        background: #fff;
        outline: none;
        transition: all 0.15s;
      }
      .pin-box:focus {
        border-color: #fbbf24;
        box-shadow: 0 0 0 2px rgba(253, 230, 138, 0.6);
      }
    `,
  ],
})
export class OtpBoxesComponent implements OnInit {
  @Input() length = 4;
  @Output() codeChange = new EventEmitter<string>();

  private readonly boxes = viewChildren<ElementRef<HTMLInputElement>>('box');

  digits: string[] = [];
  indices: number[] = [];

  ngOnInit(): void {
    this.indices = Array.from({ length: this.length }, (_, i) => i);
    this.digits = Array(this.length).fill('');
  }

  onInput(index: number, event: Event): void {
    const val = (event.target as HTMLInputElement).value.replace(/\D/g, '').slice(-1);
    this.digits[index] = val;
    if (val && index < this.length - 1) this.boxes()[index + 1]?.nativeElement.focus();
    this.codeChange.emit(this.digits.join(''));
  }

  onKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.digits[index] && index > 0)
      this.boxes()[index - 1]?.nativeElement.focus();
  }

  /** Reset all boxes to empty and refocus the first one — used before resending a code. */
  clear(): void {
    this.digits = Array(this.length).fill('');
    this.codeChange.emit('');
    this.boxes()[0]?.nativeElement.focus();
  }
}
