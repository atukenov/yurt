import { Component, inject } from '@angular/core';
import { StoreStatusService } from '../core/store-status.service';
import { TranslatePipe } from '../core/translate.pipe';

@Component({
  selector: 'app-closed-notice',
  standalone: true,
  imports: [TranslatePipe],
  styles: [`
    .wrap {
      position: fixed; left: 0; right: 0; z-index: 90; display: flex; justify-content: center;
      top: max(14px, env(safe-area-inset-top)); padding: 0 16px; pointer-events: none;
    }
    .pill {
      pointer-events: auto; display: flex; align-items: center; gap: 12px; max-width: 360px; width: 100%;
      background: #1c1917; color: #fff; border-radius: 22px; padding: 12px 14px;
      box-shadow: 0 18px 36px -12px rgba(28,25,23,.55), 0 0 0 1px rgba(255,255,255,.06) inset;
      animation: drop .38s cubic-bezier(.2,1.2,.4,1) both;
    }
    .icon {
      flex: none; width: 38px; height: 38px; border-radius: 14px; display: grid; place-items: center;
      background: #ffd119; font-size: 19px;
    }
    .title { font-size: 14px; font-weight: 800; line-height: 1.2; }
    .sub { font-size: 12px; font-weight: 500; color: #d6d3d1; margin-top: 2px; }
    .x { margin-left: auto; background: none; border: 0; color: #a8a29e; font-size: 20px; line-height: 1; cursor: pointer; padding: 4px; }
    @keyframes drop { from { opacity: 0; transform: translateY(-18px) scale(.96); } to { opacity: 1; transform: none; } }
    @media (prefers-reduced-motion: reduce) { .pill { animation: none; } }
  `],
  template: `
    @if (status.noticeVisible()) {
      <div class="wrap" role="alert" aria-live="assertive">
        <div class="pill">
          <span class="icon" aria-hidden="true">🌙</span>
          <div>
            <div class="title">{{ 'closed.title' | t }}</div>
            <div class="sub">
              @if (status.nextOpenAt(); as next) {
                {{ 'loc.opensAt' | t }} {{ next }}
              } @else {
                {{ 'closed.hint' | t }}
              }
            </div>
          </div>
          <button class="x" type="button" (click)="status.dismissNotice()" aria-label="Close">&times;</button>
        </div>
      </div>
    }
  `,
})
export class ClosedNoticeComponent {
  readonly status = inject(StoreStatusService);
}
