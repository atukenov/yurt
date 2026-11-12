import { inject, Pipe, PipeTransform } from '@angular/core';
import { PosLangService } from './lang.service';

@Pipe({ name: 't', standalone: true, pure: false })
export class PosTranslatePipe implements PipeTransform {
  private lang = inject(PosLangService);
  transform(key: string, params?: Record<string, string | number>): string {
    return this.lang.t(key, params);
  }
}
