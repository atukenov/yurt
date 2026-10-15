import { Component, Input, Output, EventEmitter, HostBinding } from '@angular/core';
import { CommonModule } from '@angular/common';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'destructive';
export type ButtonSize = 'sm' | 'md' | 'lg';

@Component({
  selector: 'yurt-button',
  standalone: true,
  imports: [CommonModule],
  template: `
    <button
      [type]="type"
      [disabled]="disabled || loading"
      [class]="computedClass"
      [style.padding]="computedPadding"
      (click)="clicked.emit($event)"
    >
      <span
        *ngIf="loading"
        class="inline-block w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin mr-2"
      ></span>
      <ng-content></ng-content>
    </button>
  `,
})
export class ButtonComponent {
  @Input() variant: ButtonVariant = 'primary';
  @Input() size: ButtonSize = 'md';
  @Input() disabled = false;
  @Input() loading = false;
  @Input() fullWidth = false;
  @Input() type: 'button' | 'submit' | 'reset' = 'button';
  @Output() clicked = new EventEmitter<MouseEvent>();

  @HostBinding('style.display')
  get hostDisplay(): string {
    return this.fullWidth ? 'block' : 'inline-block';
  }

  @HostBinding('style.width')
  get hostWidth(): string {
    return this.fullWidth ? '100%' : 'auto';
  }

  get computedPadding(): string {
    const p: Record<ButtonSize, string> = {
      sm: '0.375rem 0.75rem',
      md: '0.625rem 1.25rem',
      lg: '0.875rem 1.5rem',
    };
    return p[this.size];
  }

  get computedClass(): string {
    const base =
      'inline-flex items-center justify-center font-semibold rounded-2xl transition-all focus:outline-none focus:ring-2 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed';

    const variants: Record<ButtonVariant, string> = {
      primary: 'bg-amber-500 text-stone-900 hover:bg-amber-600 focus:ring-amber-400',
      secondary: 'bg-stone-100 text-stone-800 hover:bg-stone-200 focus:ring-stone-300',
      ghost: 'bg-transparent text-stone-700 hover:bg-stone-100 focus:ring-stone-300',
      destructive: 'bg-red-500 text-white hover:bg-red-600 focus:ring-red-400',
    };

    const sizes: Record<ButtonSize, string> = {
      sm: 'text-sm',
      md: 'text-base',
      lg: 'text-lg',
    };

    const width = this.fullWidth ? 'w-full' : '';
    return [base, variants[this.variant], sizes[this.size], width].filter(Boolean).join(' ');
  }
}
