import { Pipe, PipeTransform, inject } from '@angular/core';
import { LangService } from './lang.service';

@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private lang = inject(LangService);
  /** `'key' | t`, or `'key' | t: { count: n, name: x }` to fill {slots} and pick a plural form. */
  transform(key: string, params?: Record<string, string | number>): string {
    return this.lang.t(key, params);
  }
}
