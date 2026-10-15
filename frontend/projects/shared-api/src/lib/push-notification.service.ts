import { Platform } from '@angular/cdk/platform';
import { inject, Injectable } from '@angular/core';
import { Capacitor } from '@capacitor/core';
import { PushNotifications } from '@capacitor/push-notifications';
import { FCM } from '@capacitor-community/fcm';
import { distinctUntilChanged } from 'rxjs';
import { AuthStateService } from './auth-state.service';
import { YurtApiService } from './yurt-api.service';

@Injectable({ providedIn: 'root' })
export class PushNotificationService {
  private platform = inject(Platform);
  private auth = inject(AuthStateService);
  private api = inject(YurtApiService);
  private initialized = false;
  private lastToken: string | null = null;

  initialize(): void {
    if (this.initialized) return;
    this.initialized = true;

    if (!this.platform.IOS && !this.platform.ANDROID) return;

    PushNotifications.addListener('registration', async (apnsOrFcmToken) => {
      try {
        const isIos = Capacitor.getPlatform() === 'ios';
        // iOS delivers the raw APNs device token here; the FCM token is only
        // retrievable once Messaging.apnsToken has been set from it.
        const token = isIos ? (await FCM.getToken()).token : apnsOrFcmToken.value;
        this.lastToken = token;
        console.log('FCM token:', token);
        this.api.registerDeviceToken(token, isIos ? 'ios' : 'android').subscribe({
          error: (error) => console.error('Failed to save push token:', error),
        });
      } catch (error) {
        console.error('Failed to get push token:', error);
      }
    });

    PushNotifications.addListener('registrationError', (error) => {
      console.error('Push notification registration failed:', error);
    });

    // SignalR-driven local notifications already cover the foreground case
    // (see OrderNotificationService); acting on the raw push here too would
    // double-notify. When backgrounded/closed, iOS/Android display the FCM
    // notification payload on their own — no client code runs.
    PushNotifications.addListener('pushNotificationReceived', (notification) => {
      console.log('Push notification received:', notification);
    });

    this.auth.user$.pipe(distinctUntilChanged((a, b) => a?.userId === b?.userId)).subscribe((user) => {
      if (user) {
        this.registerForPush();
      } else if (this.lastToken) {
        this.api.unregisterDeviceToken(this.lastToken).subscribe({ error: () => {} });
        this.lastToken = null;
      }
    });
  }

  private async registerForPush(): Promise<void> {
    try {
      const permission = await PushNotifications.requestPermissions();
      if (permission.receive !== 'granted') {
        console.log('Push notification permission not granted');
        return;
      }
      await PushNotifications.register();
    } catch (error) {
      console.error('Failed to register for push notifications:', error);
    }
  }
}
