import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AppResumeService {
  readonly resumed$ = new Subject<void>();

  emit(): void {
    this.resumed$.next();
  }
}
