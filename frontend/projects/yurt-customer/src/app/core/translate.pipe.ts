import { Pipe, PipeTransform, inject } from '@angular/core';
import { LangService } from './lang.service';

@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private lang = inject(LangService);
  transform(key: string): string {
    return this.lang.t(key);
  }
}
