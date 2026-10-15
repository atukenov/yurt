import { Component, inject } from '@angular/core';
import { AppUpdateService } from '../../core/app-update.service';
import { TranslatePipe } from '../../core/translate.pipe';

/**
 * Update popup. Mandatory updates (build below the minimum version) can't be dismissed;
 * otherwise it's a bottom sheet listing what's new with a "Maybe later" option.
 */
@Component({
  selector: 'app-update-gate',
  standalone: true,
  imports: [TranslatePipe],
  styles: [`
    .backdrop { position: fixed; inset: 0; z-index: 9999; background: rgba(28,25,23,.55); backdrop-filter: blur(2px);
      display: flex; align-items: flex-end; justify-content: center; animation: fade .25s ease both; }
    .sheet { width: 100%; max-width: 480px; background: #fff; border-radius: 28px 28px 0 0;
      padding: 10px 22px calc(22px + env(safe-area-inset-bottom)); box-shadow: 0 -20px 50px -20px rgba(0,0,0,.35);
      animation: rise .4s cubic-bezier(.2,1,.3,1) both; max-height: 88vh; overflow-y: auto; }
    .grab { width: 38px; height: 4px; border-radius: 4px; background: #e7e5e4; margin: 0 auto 18px; }
    .hero { position: relative; width: 76px; height: 76px; margin: 0 auto 14px; border-radius: 26px;
      background: linear-gradient(145deg, #ffd119, #ffb300); display: grid; place-items: center; font-size: 36px;
      box-shadow: 0 14px 26px -10px rgba(255,179,0,.7); }
    .hero::after { content: '✦'; position: absolute; top: -8px; right: -6px; font-size: 20px; color: #ffb300; }
    h2 { font-size: 21px; font-weight: 800; color: #1c1917; text-align: center; letter-spacing: -.02em; }
    .chip { display: table; margin: 8px auto 0; font-size: 12px; font-weight: 700; color: #92400e; background: #fef3c7;
      border-radius: 999px; padding: 4px 12px; }
    .body { margin-top: 10px; font-size: 13.5px; color: #78716c; text-align: center; }
    .whats { margin-top: 18px; background: #fafaf9; border: 1px solid #f0eee9; border-radius: 20px; padding: 14px 16px; }
    .whats-title { font-size: 11px; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; color: #a8a29e; margin-bottom: 10px; }
    ul { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 10px; }
    li { display: flex; gap: 10px; align-items: flex-start; font-size: 14px; font-weight: 600; color: #292524; line-height: 1.35; }
    .tick { flex: none; width: 20px; height: 20px; margin-top: 1px; border-radius: 50%; background: #ffd119; display: grid; place-items: center; }
    .cta { display: flex; align-items: center; justify-content: center; gap: 8px; width: 100%; margin-top: 18px; padding: 15px;
      border-radius: 18px; background: #1c1917; color: #fff; font-size: 15px; font-weight: 800; text-decoration: none;
      box-shadow: 0 12px 22px -10px rgba(28,25,23,.6); transition: transform .15s ease; }
    .cta:active { transform: scale(.97); }
    .later { display: block; width: 100%; margin-top: 6px; padding: 12px; border: 0; background: none; font-size: 13.5px; font-weight: 700; color: #a8a29e; cursor: pointer; }
    @keyframes rise { from { transform: translateY(100%); } to { transform: none; } }
    @keyframes fade { from { opacity: 0; } to { opacity: 1; } }
    @media (prefers-reduced-motion: reduce) { .sheet, .backdrop { animation: none; } }
  `],
  template: `
    @if (svc.visible()) {
      <div class="backdrop" (click)="svc.dismiss()">
        <div class="sheet" role="dialog" aria-modal="true" aria-labelledby="update-title" (click)="$event.stopPropagation()">
          <div class="grab" aria-hidden="true"></div>
          <div class="hero" aria-hidden="true">🚀</div>
          <h2 id="update-title">{{ (svc.updateRequired() ? 'update.title' : 'update.available') | t }}</h2>
          @if (svc.latestVersion()) {
            <span class="chip">v{{ svc.latestVersion() }}@if (svc.currentVersion()) { · {{ 'update.youHave' | t }} {{ svc.currentVersion() }} }</span>
          }
          @if (svc.updateRequired()) {
            <p class="body">{{ 'update.body' | t }}</p>
          }
          @if (svc.notes().length) {
            <div class="whats">
              <div class="whats-title">{{ 'update.whatsNew' | t }}</div>
              <ul>
                @for (n of svc.notes(); track $index) {
                  <li>
                    <span class="tick" aria-hidden="true">
                      <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="#1c1917" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12l5 5 9-10"/></svg>
                    </span>
                    <span>{{ n }}</span>
                  </li>
                }
              </ul>
            </div>
          }
          <a class="cta" [href]="svc.storeUrl()">
            {{ 'update.cta' | t }}
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"/></svg>
          </a>
          @if (!svc.updateRequired()) {
            <button type="button" class="later" (click)="svc.dismiss()">{{ 'update.later' | t }}</button>
          }
        </div>
      </div>
    }
  `,
})
export class AppUpdateGateComponent {
  readonly svc = inject(AppUpdateService);
}
