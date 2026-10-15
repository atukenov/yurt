import { Injectable, signal } from '@angular/core';
import { Promotion } from 'shared-models';

@Injectable({ providedIn: 'root' })
export class PromoViewerService {
  readonly openRequest = signal<{ promos: Promotion[]; startIndex: number } | null>(null);
}
