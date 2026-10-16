import { Component, effect, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { BrandService, ModuleService, ThemeService, YurtApiService } from 'shared-api';
import { ToastContainerComponent } from 'shared-ui';
import { environment } from '../environments/environment';
import { ConfirmDialogComponent } from './shared/confirm-dialog/confirm-dialog.component';

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
  private theme = inject(ThemeService); // applies the saved light/dark choice

  constructor() {
    effect(() => { document.title = `${this.brand.name()} Admin`; });
  }

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.modules.load();
  }
}