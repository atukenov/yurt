import { Component, DestroyRef, effect, inject, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { distinctUntilChanged, map, skip } from 'rxjs';
import { AuthStateService, AuthUser, BrandService, ModuleService, TENANT_SLUG, ThemeService, YurtApiService } from 'shared-api';
import { ToastContainerComponent } from 'shared-ui';
import { environment } from '../environments/environment';
import { ConfirmDialogComponent } from './shared/confirm-dialog/confirm-dialog.component';

/** Whose look the panel wears: a cafe admin's cafe, or null for the plain Yurt look (signed out, platform operator). */
export function brandOwner(user: AuthUser | null, appCafe: string | null): string | null {
  if (user?.role === 'SuperAdmin') return null;
  if (user) return user.cafe ?? appCafe ?? '';
  return appCafe;
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastContainerComponent, ConfirmDialogComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  private api = inject(YurtApiService);
  private modules = inject(ModuleService);
  private brand = inject(BrandService);
  private auth = inject(AuthStateService);
  private appCafe = inject(TENANT_SLUG);
  private destroyRef = inject(DestroyRef);
  private theme = inject(ThemeService); // applies the saved light/dark choice

  constructor() {
    effect(() => { document.title = `${this.brand.name()} Admin`; });
  }

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    const owner$ = this.auth.user$.pipe(map((u) => brandOwner(u, this.appCafe)), distinctUntilChanged());
    owner$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((owner) => this.brand.useNeutral(owner === null));
    this.modules.load();
    // Signing in, out or as someone from another cafe changes which cafe the requests are for: fetch that cafe's
    // modules and brand again.
    this.auth.user$.pipe(map((u) => `${u?.role ?? ''}|${u?.cafe ?? ''}`), distinctUntilChanged(), skip(1), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.modules.load());
  }
}
