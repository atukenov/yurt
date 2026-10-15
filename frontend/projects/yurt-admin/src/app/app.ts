import { Component, inject, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ModuleService, YurtApiService } from 'shared-api';
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

  ngOnInit(): void {
    this.api.configure(environment.apiUrl);
    this.modules.load();
  }
}