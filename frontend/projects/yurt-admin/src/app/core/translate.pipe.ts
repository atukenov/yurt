import { inject, Pipe, PipeTransform } from '@angular/core';
import { AdminLangService } from './lang.service';

@Pipe({ name: 't', standalone: true, pure: false })
export class AdminTranslatePipe implements PipeTransform {
  private lang = inject(AdminLangService);
  /** `'key' | t`, or `'key' | t: { count: n, name: x }` to fill {slots} and pick a plural form. */
  transform(key: string, params?: Record<string, string | number>): string {
    return this.lang.t(key, params);
  }
}
