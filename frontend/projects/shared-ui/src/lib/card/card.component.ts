import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'yurt-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div [class]="computedClass">
      <ng-content></ng-content>
    </div>
  `,
})
export class CardComponent {
  @Input() padding: 'sm' | 'md' | 'lg' = 'md';
  @Input() hover = false;

  get computedClass(): string {
    const base = 'bg-white rounded-2xl shadow-sm border border-stone-100';
    const paddings = { sm: 'p-3', md: 'p-4', lg: 'p-6' };
    const hoverClass = this.hover ? 'hover:shadow-md transition-shadow cursor-pointer' : '';
    return [base, paddings[this.padding], hoverClass].filter(Boolean).join(' ');
  }
}
