import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Location } from '@angular/common';
import { Router } from '@angular/router';
import jsQR from 'jsqr';
import { TranslatePipe } from '../../core/translate.pipe';
import { tableCodeFrom } from '../../core/table.service';

/** How often a camera frame is checked for a QR code. */
const SCAN_EVERY_MS = 150;
/** A table code is 10 letters and digits (see TableService on the server). */
const CODE_RE = /^[A-Z0-9]{6,16}$/;

/**
 * Scans the QR code on a table with the phone camera (/scan-table), in the installed app and on the web. The phone's
 * own camera app opens a table QR in the browser, so a guest who orders in the installed app scans it here instead.
 * A code that is found opens /t/<code>, the same page a QR link opens. The code can also be typed.
 */
@Component({
  selector: 'app-table-scan',
  standalone: true,
  imports: [FormsModule, TranslatePipe],
  template: `
    <div class="fixed inset-0 flex flex-col" style="background:#0c0a09;color:#fff">
      <div class="flex items-center justify-between" style="padding:calc(env(safe-area-inset-top) + 14px) 16px 10px">
        <h1 style="margin:0;font-size:17px;font-weight:800">{{ 'table.scanTitle' | t }}</h1>
        <button (click)="close()" data-test="scan-close" [attr.aria-label]="'table.scanClose' | t"
          style="width:40px;height:40px;border-radius:999px;border:none;background:rgba(255,255,255,.15);color:#fff;font-size:20px;cursor:pointer">✕</button>
      </div>

      <div class="relative flex-1 overflow-hidden flex items-center justify-center">
        <video #video playsinline muted autoplay class="absolute inset-0 w-full h-full" style="object-fit:cover"
          [style.display]="cameraOn() ? 'block' : 'none'"></video>
        @if (cameraOn()) {
          <div aria-hidden="true" style="position:relative;width:min(70vw,280px);aspect-ratio:1;border-radius:24px;box-shadow:0 0 0 9999px rgba(0,0,0,.45);border:3px solid var(--brand-500)"></div>
        } @else {
          <p style="max-width:300px;text-align:center;font-size:14px;font-weight:600;line-height:1.5;padding:0 20px;opacity:.85">
            {{ (cameraFailed() ? 'table.cameraBlocked' : 'table.cameraStarting') | t }}
          </p>
        }
      </div>

      <div style="padding:14px 16px calc(env(safe-area-inset-bottom) + 18px);background:#1c1917">
        <p style="margin:0 0 10px;font-size:13px;font-weight:600;opacity:.8;text-align:center">{{ 'table.scanHint' | t }}</p>
        <form (ngSubmit)="useTyped()" style="display:flex;gap:8px">
          <input name="tableCode" [ngModel]="typed()" (ngModelChange)="typed.set($event)"
            [attr.aria-label]="'table.codePrompt' | t" [placeholder]="'table.codePrompt' | t"
            autocapitalize="characters" autocomplete="off" spellcheck="false" data-test="scan-code"
            style="flex:1;min-width:0;font-size:15px;font-weight:600;color:var(--ink-900);background:#fff;border:none;border-radius:999px;padding:11px 16px;font-family:inherit;outline:none">
          <button type="submit" [disabled]="!typed().trim()" data-test="scan-code-ok"
            style="font-size:14px;font-weight:800;color:var(--brand-on-primary);background:var(--brand-500);border:none;border-radius:999px;padding:0 20px;cursor:pointer;font-family:inherit"
            class="disabled:opacity-50">{{ 'table.codeJoin' | t }}</button>
        </form>
      </div>
    </div>
  `,
})
export class TableScanComponent implements OnInit, OnDestroy {
  private router = inject(Router);
  private location = inject(Location);

  @ViewChild('video', { static: true }) private videoRef!: ElementRef<HTMLVideoElement>;

  readonly cameraOn = signal(false);
  readonly cameraFailed = signal(false);
  readonly typed = signal('');

  private stream: MediaStream | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;
  private canvas = document.createElement('canvas');
  private done = false;

  async ngOnInit(): Promise<void> {
    try {
      if (!navigator.mediaDevices?.getUserMedia) throw new Error('no camera');
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' } },
        audio: false,
      });
      if (this.done) return this.stop();
      const video = this.videoRef.nativeElement;
      video.srcObject = this.stream;
      await video.play().catch(() => undefined);
      this.cameraOn.set(true);
      this.timer = setInterval(() => this.scanFrame(), SCAN_EVERY_MS);
    } catch {
      this.cameraFailed.set(true);
    }
  }

  ngOnDestroy(): void {
    this.done = true;
    this.stop();
  }

  close(): void {
    this.stop();
    if (window.history.length > 1) this.location.back();
    else this.router.navigateByUrl('/menu', { replaceUrl: true });
  }

  useTyped(): void {
    const code = tableCodeFrom(this.typed());
    if (code) this.open(code);
  }

  private scanFrame(): void {
    const video = this.videoRef.nativeElement;
    const w = video.videoWidth;
    const h = video.videoHeight;
    if (!w || !h || this.done) return;
    // Scan a smaller copy: fast enough on older phones, and a table sticker fills much of the frame anyway.
    const scale = Math.min(1, 640 / Math.max(w, h));
    this.canvas.width = Math.round(w * scale);
    this.canvas.height = Math.round(h * scale);
    const ctx = this.canvas.getContext('2d', { willReadFrequently: true });
    if (!ctx) return;
    ctx.drawImage(video, 0, 0, this.canvas.width, this.canvas.height);
    const image = ctx.getImageData(0, 0, this.canvas.width, this.canvas.height);
    const found = jsQR(image.data, image.width, image.height, { inversionAttempts: 'dontInvert' });
    if (!found?.data) return;
    // Only a table QR (…/t/CODE) or a bare code; any other QR (a menu link, Wi-Fi) is ignored and scanning goes on.
    const isLink = /\/t\/[A-Za-z0-9]+/.test(found.data);
    const code = tableCodeFrom(found.data);
    if (isLink || CODE_RE.test(code)) this.open(code);
  }

  private open(code: string): void {
    this.done = true;
    this.stop();
    this.router.navigateByUrl(`/t/${encodeURIComponent(code)}`, { replaceUrl: true });
  }

  private stop(): void {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
    this.cameraOn.set(false);
  }
}
