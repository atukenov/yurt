import { Platform } from '@angular/cdk/platform';
import { inject, Injectable } from '@angular/core';
import { LocalNotifications, LocalNotificationSchema } from '@capacitor/local-notifications';

export interface NotificationOptions {
  title: string;
  body: string;
  tag?: string;
  badge?: number;
  sound?: string;
  largeBody?: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private platform = inject(Platform);
  private isInitialized = false;

  async initialize(): Promise<void> {
    if (this.isInitialized) return;

    if (!this.platform.IOS && !this.platform.ANDROID) {
      console.warn('Notifications only available on mobile platforms');
      return;
    }

    try {
      // Request notification permissions
      const permission = await LocalNotifications.requestPermissions();

      if (permission.display === 'granted') {
        this.isInitialized = true;
        console.log('Notification permissions granted');
      }
    } catch (error) {
      console.error('Failed to initialize notifications:', error);
    }
  }

  async sendNotification(options: NotificationOptions): Promise<void> {
    if (!this.isInitialized && (this.platform.IOS || this.platform.ANDROID)) {
      await this.initialize();
    }

    if (!this.platform.IOS && !this.platform.ANDROID) {
      // Web fallback - show browser notification if available
      this.showWebNotification(options);
      return;
    }

    try {
      const notification: LocalNotificationSchema = {
        title: options.title,
        body: options.body,
        id: Math.floor(Math.random() * 1000000),
        extra: { tag: options.tag || 'order-notification' },
        largeBody: options.largeBody,
        sound: options.sound || 'default',
      };

      if (this.platform.ANDROID) {
        notification.channelId = 'order-notifications';
        notification.smallIcon = 'ic_notification';
      }

      await LocalNotifications.schedule({ notifications: [notification] });
    } catch (error) {
      console.error('Failed to send notification:', error);
    }
  }

  private showWebNotification(options: NotificationOptions): void {
    if ('Notification' in window && Notification.permission === 'granted') {
      new Notification(options.title, {
        body: options.body,
        tag: options.tag || 'order-notification',
      });
    }
  }

  async requestWebNotificationPermission(): Promise<void> {
    if ('Notification' in window && Notification.permission === 'default') {
      await Notification.requestPermission();
    }
  }
}
