import { Component, effect, inject, OnDestroy, OnInit } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Capacitor } from '@capacitor/core';
import { StatusBar, Style } from '@capacitor/status-bar';
import {
  AuthStateService,
  BrandService,
  ThemeService,
  ModuleService,
  NotificationService,
  OrderNotificationService,
  PushNotificationService,
  SignalrService,
  YurtApiService,
} from 'shared-api';
import { environment } from '../environments/environment';
import { SplashComponent } from './features/loading/splash.component';
import { AppUpdateGateComponent } from './features/app-update/app-update-gate.component';
import { ClosedNoticeComponent } from './shared/closed-notice.component';
import { MaintenanceNoticeComponent } from './shared/maintenance-notice.component';
import { AppStateService } from './core/app-state.service';
import { AppResumeService } from './core/app-resume.service';
import { AppUpdateService } from './core/app-update.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SplashComponent, AppUpdateGateComponent, ClosedNoticeComponent, MaintenanceNoticeComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit, OnDestroy {
  private api = inject(YurtApiService);
  private modules = inject(ModuleService);
  private brand = inject(BrandService);
  private theme = inject(ThemeService);
  private auth = inject(AuthStateService);
  private signalr = inject(SignalrService);
  private orderNotifications = inject(OrderNotificationService);
  private notifications = inject(NotificationService);
  private pushNotifications = inject(PushNotificationService);
  private appState = inject(AppStateService);
  private appResume = inject(AppResumeService);
  private appUpdate = inject(AppUpdateService);

  readonly appReady = this.appState.refreshReady;

  constructor() {
    effect(() => { document.title = this.brand.name(); });
  }

  ngOnInit(): void {
    if (Capacitor.isNativePlatform()) {
      StatusBar.setOverlaysWebView({ overlay: true });
      StatusBar.setStyle({ style: Style.Light });
    }

    this.api.configure(environment.apiUrl);
    this.signalr.configure(environment.apiUrl);
    this.modules.load();
    this.appUpdate.check();

    if (this.auth.isLoggedIn) {
      const rt = this.auth.refreshToken;
      if (rt) {
        this.api.refreshToken(rt).subscribe({
          next: (res) => {
            this.auth.setUser({
              accessToken: res.accessToken,
              refreshToken: res.refreshToken,
              userId: res.userId,
              displayName: res.displayName,
              userType: res.userType,
            });
            this.appState.setReady();
          },
          error: (err) => {
            if (err.status === 401 || err.status === 403) {
              this.auth.logout();
            }
            this.appState.setReady();
          },
        });
      } else {
        this.appState.setReady();
      }
    } else {
      this.appState.setReady();
    }
    this.notifications.initialize();
    if (this.modules.isEnabled('push')) this.pushNotifications.initialize();

    // Initialize SignalR connection and order notifications
    this.signalr
      .startConnection()
      .then(() => {
        this.orderNotifications.initialize();
      })
      .catch((error) => {
        console.error('Failed to start SignalR connection:', error);
      });

    // Re-connect SignalR and notify pages when app comes back to foreground
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        this.signalr.startConnection().catch(() => {});
        this.appResume.emit();
      }
    });
  }

  ngOnDestroy(): void {
    this.signalr.stopConnection();
  }
}
