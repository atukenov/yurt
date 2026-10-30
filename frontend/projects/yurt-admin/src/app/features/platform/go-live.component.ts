import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { YurtApiService } from 'shared-api';
import { Cafe, GoLiveChecklist } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { goLiveGuide, goLiveProgress, isCodeName } from './go-live';

/**
 * Read-only go-live checklist: the server settings every cafe shares, then what the chosen cafe still needs. Shows
 * only done or missing (and the names of what is missing), never a secret value. Fixing happens on the Platform page.
 */
@Component({
  selector: 'app-go-live',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, AdminTranslatePipe],
  templateUrl: './go-live.component.html',
})
export class GoLiveComponent implements OnInit {
  private api = inject(YurtApiService);

  cafes = signal<Cafe[]>([]);
  /** Slug of the chosen cafe; '' = the cafe of this address. */
  cafe = '';
  checklist = signal<GoLiveChecklist | null>(null);
  loading = signal(true);
  failed = signal(false);

  progress = computed(() => goLiveProgress(this.checklist()));

  readonly guide = goLiveGuide;
  readonly isCodeName = isCodeName;

  ngOnInit(): void {
    this.api.getCafes().subscribe({ next: (c) => this.cafes.set(c) });
    this.load();
  }

  selectCafe(slug: string): void {
    this.cafe = slug;
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.getGoLiveChecklist(this.cafe || null).subscribe({
      next: (c) => { this.checklist.set(c); this.loading.set(false); },
      error: () => { this.checklist.set(null); this.failed.set(true); this.loading.set(false); },
    });
  }
}
