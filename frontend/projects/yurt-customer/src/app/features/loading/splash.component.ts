import {
  AfterViewInit,
  Component,
  computed,
  ElementRef,
  input,
  signal,
  ViewChild,
} from '@angular/core';

@Component({
  selector: 'app-splash',
  standalone: true,
  template: `
    @if (!dismissed()) {
      <div class="fixed inset-0 z-9999 bg-black" style="transition: opacity 0.6s ease">
        @if (!videoFailed()) {
          <video
            #introVideo
            class="w-full h-full object-cover"
            playsinline
            webkit-playsinline
            muted
            (ended)="onVideoEnded()"
            (error)="onVideoError()"
          >
            <source src="intro.mp4" type="video/mp4" />
          </video>
        } @else {
          <!-- Fallback: amber gradient with logo if video fails to load -->
          <div class="w-full h-full flex flex-col items-center justify-center bg-linear-to-br from-amber-500 to-amber-600">
            <img src="logo.png" alt="Altyncup" class="w-28 h-28 object-contain drop-shadow-2xl" style="animation: pulse 1.2s ease-in-out infinite" />
            <style>@keyframes pulse { 0%,100%{transform:scale(1)} 50%{transform:scale(1.06)} }</style>
          </div>
        }
      </div>
    }
  `,
})
export class SplashComponent implements AfterViewInit {
  readonly ready = input.required<boolean>();

  @ViewChild('introVideo') videoRef?: ElementRef<HTMLVideoElement>;

  private readonly _videoEnded = signal(false);
  readonly videoFailed = signal(false);

  // Dismiss only when BOTH the video has finished AND the app is ready
  readonly dismissed = computed(() => this._videoEnded() && this.ready());

  ngAfterViewInit(): void {
    const video = this.videoRef?.nativeElement;
    if (!video) return;

    video.play().catch(() => {
      // Autoplay blocked or video unavailable — skip straight to fallback logic
      this.onVideoError();
    });
  }

  onVideoEnded(): void {
    this.releaseVideo();
    this._videoEnded.set(true);
  }

  onVideoError(): void {
    this.releaseVideo();
    this.videoFailed.set(true);
    this._videoEnded.set(true);
  }

  // Fully release the native video decoder (playsinline video on iOS is backed by
  // its own AVPlayerLayer) as soon as playback is done, rather than leaving it
  // mounted-but-hidden — a lingering native video layer can otherwise interfere
  // with touch-gesture dispatch to the rest of the WKWebView content.
  private releaseVideo(): void {
    const video = this.videoRef?.nativeElement;
    if (!video) return;
    video.pause();
    video.removeAttribute('src');
    video.load();
  }
}
