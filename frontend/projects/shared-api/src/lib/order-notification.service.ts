import { inject, Injectable } from '@angular/core';
import { Order, OrderStatus } from 'shared-models';
import { NotificationService } from './notification.service';
import { SignalrService } from './signalr.service';

type Lang = 'en' | 'ru' | 'kk';

@Injectable({ providedIn: 'root' })
export class OrderNotificationService {
  private signalr = inject(SignalrService);
  private notifications = inject(NotificationService);
  private lastNotifiedOrders = new Map<string, OrderStatus>();

  initialize(): void {
    this.signalr.orderCreated$.subscribe((order: Order) => {
      const s = this.getStrings();
      this.sendOrderNotification(s.orderReceived[0], s.orderReceived[1], order, 'created');
    });

    this.signalr.orderUpdated$.subscribe((order: Order) => {
      this.handleOrderUpdate(order);
    });

    this.signalr.paymentUpdated$.subscribe((order: Order) => {
      const s = this.getStrings();
      this.sendOrderNotification(s.paymentUpdated[0], s.paymentUpdated[1], order, 'payment');
    });

    this.signalr.orderDeclined$.subscribe((order: Order) => {
      const s = this.getStrings();
      const reason = (order as any).declineReason;
      const body = reason ? `${s.orderDeclined[1]}: ${reason}` : s.orderDeclined[1];
      this.sendOrderNotification(s.orderDeclined[0], body, order, 'declined');
    });

    this.signalr.etaReminder$.subscribe(({ orderId, minutesLeft }) => {
      const s = this.getStrings();
      this.notifications.sendNotification({
        title: s.etaReminderTitle,
        body: minutesLeft === 1 ? s.eta1MinLeft : s.eta5MinLeft,
        tag: `eta-${orderId}-${minutesLeft}`,
        largeBody: minutesLeft === 1 ? s.eta1MinLeft : s.eta5MinLeft,
      }).catch(() => {});
    });
  }

  private handleOrderUpdate(order: Order): void {
    const lastStatus = this.lastNotifiedOrders.get(order.id);

    if (lastStatus === order.status) return;
    this.lastNotifiedOrders.set(order.id, order.status);
    if (lastStatus === undefined) return;

    const s = this.getStrings();
    const statusText = s.statuses[order.status] ?? order.status;
    this.sendOrderNotification(
      s.orderUpdated[0],
      `${s.orderUpdated[1]} ${statusText}`,
      order,
      'status-update',
    );
  }

  private sendOrderNotification(title: string, body: string, order: Order, type: string): void {
    this.notifications
      .sendNotification({
        title,
        body,
        tag: `order-${order.id}`,
        largeBody: `Order ID: ${order.id}\n${body}`,
      })
      .catch((error) => {
        console.error('Failed to send order notification:', error);
      });
  }

  private getLang(): Lang {
    const lang = localStorage.getItem('yurt_lang');
    return lang === 'en' || lang === 'kk' ? lang : 'ru';
  }

  private getStrings(): {
    orderReceived: [string, string];
    orderUpdated: [string, string];
    orderDeclined: [string, string];
    paymentUpdated: [string, string];
    etaReminderTitle: string;
    eta5MinLeft: string;
    eta1MinLeft: string;
    statuses: Record<string, string>;
  } {
    const lang = this.getLang();
    const s = {
      en: {
        orderReceived: ['New Order', 'Your order has been received!'] as [string, string],
        orderUpdated: ['Order Updated', 'Your order is now'] as [string, string],
        orderDeclined: ['Order Declined', 'Your order was declined'] as [string, string],
        paymentUpdated: ['Payment Updated', 'Payment status updated'] as [string, string],
        etaReminderTitle: 'Your order is almost ready!',
        eta5MinLeft: '5 minutes left',
        eta1MinLeft: '1 minute left!',
        statuses: {
          [OrderStatus.Created]: 'received',
          [OrderStatus.Accepted]: 'accepted',
          [OrderStatus.Preparing]: 'being prepared',
          [OrderStatus.Ready]: 'ready for pickup',
          [OrderStatus.Completed]: 'completed',
          [OrderStatus.Declined]: 'declined',
        },
      },
      ru: {
        orderReceived: ['Новый заказ', 'Ваш заказ получен!'] as [string, string],
        orderUpdated: ['Заказ обновлён', 'Ваш заказ'] as [string, string],
        orderDeclined: ['Заказ отклонён', 'Ваш заказ отклонён'] as [string, string],
        paymentUpdated: ['Оплата', 'Статус оплаты обновлён'] as [string, string],
        etaReminderTitle: 'Ваш заказ почти готов!',
        eta5MinLeft: 'Осталось 5 минут',
        eta1MinLeft: 'Осталась 1 минута!',
        statuses: {
          [OrderStatus.Created]: 'получен',
          [OrderStatus.Accepted]: 'принят',
          [OrderStatus.Preparing]: 'готовится',
          [OrderStatus.Ready]: 'готов к выдаче',
          [OrderStatus.Completed]: 'выполнен',
          [OrderStatus.Declined]: 'отклонён',
        },
      },
      kk: {
        orderReceived: ['Жаңа тапсырыс', 'Тапсырысыңыз алынды!'] as [string, string],
        orderUpdated: ['Тапсырыс жаңартылды', 'Тапсырысыңыз'] as [string, string],
        orderDeclined: ['Тапсырыс қабылданбады', 'Тапсырысыңыз қабылданбады'] as [string, string],
        paymentUpdated: ['Төлем', 'Төлем күйі жаңартылды'] as [string, string],
        etaReminderTitle: 'Тапсырысыңыз дайын болуға жақын!',
        eta5MinLeft: '5 минут қалды',
        eta1MinLeft: '1 минут қалды!',
        statuses: {
          [OrderStatus.Created]: 'алынды',
          [OrderStatus.Accepted]: 'қабылданды',
          [OrderStatus.Preparing]: 'дайындалуда',
          [OrderStatus.Ready]: 'дайын',
          [OrderStatus.Completed]: 'аяқталды',
          [OrderStatus.Declined]: 'қабылданбады',
        },
      },
    };
    return s[lang];
  }
}
