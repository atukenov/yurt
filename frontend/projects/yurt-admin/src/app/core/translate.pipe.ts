import { inject, Pipe, PipeTransform } from '@angular/core';
import { AdminLangService } from './lang.service';

@Pipe({ name: 't', standalone: true, pure: false })
export class AdminTranslatePipe implements PipeTransform {
  private lang = inject(AdminLangService);
  transform(key: string): string {
    return this.lang.t(key);
  }
}
