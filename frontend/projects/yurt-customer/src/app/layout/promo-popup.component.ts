import { Component, effect, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { YurtApiService } from 'shared-api';
import { Promotion } from 'shared-models';
import { environment } from '../../environments/environment';
import { PromoViewerService } from '../core/promo-viewer.service';

@Component({
  selector: 'app-promo-popup',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './promo-popup.component.html',
  styleUrl: './promo-popup.component.css',
})
export class PromoPopupComponent implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private promoViewer = inject(PromoViewerService);

  private readonly SEEN_KEY = 'yurt_seen_promos';
  private autoTimer: ReturnType<typeof setInterval> | null = null;
  private readonly STORY_DURATION = 5000;

  promotions = signal<Promotion[]>([]);
  currentIndex = signal(0);
  visible = signal(false);
  progress = signal(0);
  swipeTranslateY = signal(0);

  private swipeStartY = 0;
  private isDragging = false;

  constructor() {
    effect(() => {
      const req = this.promoViewer.openRequest();
      if (req) {
        this.clearTimer();
        this.promotions.set(req.promos);
        this.currentIndex.set(req.startIndex);
        this.progress.set(0);
        this.visible.set(true);
        this.startTimer();
        this.promoViewer.openRequest.set(null);
      }
    });
  }

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.api.getActivePromotions().subscribe({
      next: (all) => {
        const seen = this.loadSeen();
        const unseen = all.filter((p) => !seen.has(p.id));
        if (unseen.length > 0) {
          this.promotions.set(unseen);
          this.visible.set(true);
          this.startTimer();
        }
      },
    });
  }

  ngOnDestroy(): void {
    this.clearTimer();
  }

  get current(): Promotion | null {
    return this.promotions()[this.currentIndex()] ?? null;
  }

  segmentWidth(i: number): number {
    const cur = this.currentIndex();
    if (i < cur) return 100;
    if (i === cur) return this.progress();
    return 0;
  }

  onSwipeStart(e: TouchEvent): void {
    this.swipeStartY = e.touches[0].clientY;
    this.isDragging = false;
    this.swipeTranslateY.set(0);
  }

  onSwipeMove(e: TouchEvent): void {
    const dy = e.touches[0].clientY - this.swipeStartY;
    if (dy > 10) {
      this.isDragging = true;
      this.swipeTranslateY.set(Math.max(dy, 0));
    }
  }

  onSwipeEnd(e: TouchEvent): void {
    const dy = e.changedTouches[0].clientY - this.swipeStartY;
    if (dy > 80) {
      this.swipeTranslateY.set(0);
      this.closeAll();
    } else {
      this.swipeTranslateY.set(0);
    }
  }

  onTap(event: MouseEvent): void {
    if (this.isDragging) return;
    event.clientX > window.innerWidth / 2 ? this.next() : this.prev();
  }

  prev(): void {
    const i = this.currentIndex();
    if (i > 0) {
      this.clearTimer();
      this.currentIndex.set(i - 1);
      this.startTimer();
    }
  }

  next(): void {
    const promos = this.promotions();
    const i = this.currentIndex();
    this.markSeen(promos[i].id);
    this.clearTimer();
    if (i + 1 < promos.length) {
      this.currentIndex.set(i + 1);
      this.startTimer();
    } else {
      this.visible.set(false);
    }
  }

  closeAll(): void {
    this.promotions().forEach((p) => this.markSeen(p.id));
    this.clearTimer();
    this.visible.set(false);
  }

  private startTimer(): void {
    this.progress.set(0);
    let elapsed = 0;
    this.autoTimer = setInterval(() => {
      elapsed += 50;
      this.progress.set(Math.min((elapsed / this.STORY_DURATION) * 100, 100));
      if (elapsed >= this.STORY_DURATION) this.next();
    }, 50);
  }

  private clearTimer(): void {
    if (this.autoTimer) {
      clearInterval(this.autoTimer);
      this.autoTimer = null;
    }
  }

  private loadSeen(): Set<string> {
    try {
      return new Set(JSON.parse(localStorage.getItem(this.SEEN_KEY) ?? '[]'));
    } catch {
      return new Set();
    }
  }

  private markSeen(id: string): void {
    const seen = this.loadSeen();
    seen.add(id);
    localStorage.setItem(this.SEEN_KEY, JSON.stringify([...seen]));
  }
}
