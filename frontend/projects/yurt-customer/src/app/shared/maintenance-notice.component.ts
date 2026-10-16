import { Component, inject } from '@angular/core';
import { ModuleService } from 'shared-api';
import { TranslatePipe } from '../core/translate.pipe';

/** Blocks the app while the operator has maintenance mode on (the API answers 503 for customer calls anyway). */
@Component({
  selector: 'app-maintenance-notice',
  standalone: true,
  imports: [TranslatePipe],
  styles: [`
    .overlay {
      position: fixed; inset: 0; z-index: 100; display: grid; place-items: center; padding: 24px;
      background: var(--ink-50); text-align: center;
    }
    .box { max-width: 340px; }
    .icon { font-size: 48px; }
    h1 { font-size: 20px; font-weight: 800; margin: 12px 0 6px; color: var(--ink-900); }
    p { font-size: 14px; color: var(--ink-600); margin: 0; }
  `],
  template: `
    @if (modules.maintenance(); as m) {
      <div class="overlay" role="alert" aria-live="assertive">
        <div class="box">
          <div class="icon" aria-hidden="true">🛠️</div>
          <h1>{{ 'maint.title' | t }}</h1>
          <p>{{ m.message || ('maint.default' | t) }}</p>
        </div>
      </div>
    }
  `,
})
export class MaintenanceNoticeComponent {
  readonly modules = inject(ModuleService);
}
