import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type BadgeVariant = 'amber' | 'green' | 'red' | 'blue' | 'slate' | 'teal';

@Component({
  selector: 'yurt-badge',
  standalone: true,
  imports: [CommonModule],
  template: `
    <span [class]="computedClass">
      <ng-content></ng-content>
    </span>
  `,
})
export class BadgeComponent {
  @Input() variant: BadgeVariant = 'slate';

  get computedClass(): string {
    const base = 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold';
    const variants: Record<BadgeVariant, string> = {
      amber: 'bg-amber-100 text-amber-800',
      green: 'bg-green-100 text-green-800',
      red: 'bg-red-100 text-red-800',
      blue: 'bg-blue-100 text-blue-800',
      slate: 'bg-slate-100 text-slate-700',
      teal: 'bg-teal-100 text-teal-800',
    };
    return [base, variants[this.variant]].join(' ');
  }
}
