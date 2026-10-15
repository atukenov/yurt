import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AppStateService {
  readonly refreshReady = signal(false);

  setReady(): void {
    this.refreshReady.set(true);
  }
}
