import { Component, DestroyRef, effect, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { distinctUntilChanged, map, skip } from 'rxjs';
import { AuthStateService, BrandService, ModuleService, ThemeService, YurtApiService } from 'shared-api';
import { ToastContainerComponent } from 'shared-ui';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainerComponent],
  template: `<router-outlet /><yurt-toast-container />`,
})
export class App implements OnInit {
  private api = inject(YurtApiService);
  private modules = inject(ModuleService);
  private auth = inject(AuthStateService);
  private brand = inject(BrandService); // the cafe's colours and name
  private theme = inject(ThemeService); // the saved light/dark choice
  private destroyRef = inject(DestroyRef);

  constructor() {
    effect(() => { document.title = `${this.brand.name()} POS`; });
  }

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.modules.load();
    // Signing in as staff of another cafe changes which cafe the requests are for.
    this.auth.user$.pipe(map((u) => u?.cafe ?? ''), distinctUntilChanged(), skip(1), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.modules.load());
  }
}
