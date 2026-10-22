import { Pipe, PipeTransform, inject } from '@angular/core';
import { OrderStatus, PaymentStatus } from 'shared-models';
import { BadgeVariant } from '../badge/badge.component';
import { UI_LANG, UiLang, toUiLang } from '../locale/locale-format';

const ORDER_STATUS_LABELS: Record<UiLang, Record<OrderStatus, string>> = {
  en: {
    [OrderStatus.Created]: 'Order Placed',
    [OrderStatus.Accepted]: 'Accepted',
    [OrderStatus.Preparing]: 'Preparing',
    [OrderStatus.Ready]: 'Ready for Pickup',
    [OrderStatus.Completed]: 'Completed',
    [OrderStatus.Declined]: 'Declined',
  },
  ru: {
    [OrderStatus.Created]: 'Заказ принят',
    [OrderStatus.Accepted]: 'Подтверждён',
    [OrderStatus.Preparing]: 'Готовится',
    [OrderStatus.Ready]: 'Готов к выдаче',
    [OrderStatus.Completed]: 'Завершён',
    [OrderStatus.Declined]: 'Отклонён',
  },
  kk: {
    [OrderStatus.Created]: 'Тапсырыс берілді',
    [OrderStatus.Accepted]: 'Қабылданды',
    [OrderStatus.Preparing]: 'Дайындалуда',
    [OrderStatus.Ready]: 'Дайын',
    [OrderStatus.Completed]: 'Аяқталды',
    [OrderStatus.Declined]: 'Қабылданбады',
  },
};

/** Order status in the app's language (see UI_LANG). */
@Pipe({ name: 'orderStatusLabel', standalone: true, pure: false })
export class OrderStatusLabelPipe implements PipeTransform {
  private lang = inject(UI_LANG);
  transform(status: OrderStatus): string {
    return ORDER_STATUS_LABELS[toUiLang(this.lang())][status] ?? status;
  }
}

@Pipe({ name: 'orderStatusColor', standalone: true })
export class OrderStatusColorPipe implements PipeTransform {
  transform(status: OrderStatus): BadgeVariant {
    const map: Record<OrderStatus, BadgeVariant> = {
      [OrderStatus.Created]: 'amber',
      [OrderStatus.Accepted]: 'blue',
      [OrderStatus.Preparing]: 'teal',
      [OrderStatus.Ready]: 'green',
      [OrderStatus.Completed]: 'slate',
      [OrderStatus.Declined]: 'red',
    };
    return map[status] ?? 'slate';
  }
}

const PAYMENT_STATUS_LABELS: Record<UiLang, Record<PaymentStatus, string>> = {
  en: {
    [PaymentStatus.Unpaid]: 'Unpaid',
    [PaymentStatus.Pending]: 'Pending',
    [PaymentStatus.Paid]: 'Paid',
    [PaymentStatus.Failed]: 'Failed',
    [PaymentStatus.Expired]: 'Expired',
    [PaymentStatus.Refunded]: 'Refunded',
  },
  ru: {
    [PaymentStatus.Unpaid]: 'Не оплачен',
    [PaymentStatus.Pending]: 'Ожидает оплаты',
    [PaymentStatus.Paid]: 'Оплачен',
    [PaymentStatus.Failed]: 'Ошибка оплаты',
    [PaymentStatus.Expired]: 'Срок истёк',
    [PaymentStatus.Refunded]: 'Возвращён',
  },
  kk: {
    [PaymentStatus.Unpaid]: 'Төленбеген',
    [PaymentStatus.Pending]: 'Төлем күтілуде',
    [PaymentStatus.Paid]: 'Төленді',
    [PaymentStatus.Failed]: 'Төлем өтпеді',
    [PaymentStatus.Expired]: 'Мерзімі өтті',
    [PaymentStatus.Refunded]: 'Қайтарылды',
  },
};

/** Payment status in the app's language (see UI_LANG). */
@Pipe({ name: 'paymentStatusLabel', standalone: true, pure: false })
export class PaymentStatusLabelPipe implements PipeTransform {
  private lang = inject(UI_LANG);
  transform(status: PaymentStatus): string {
    return PAYMENT_STATUS_LABELS[toUiLang(this.lang())][status] ?? status;
  }
}

@Pipe({ name: 'currency2', standalone: true })
export class Currency2Pipe implements PipeTransform {
  transform(value: number): string {
    const rounded = Math.round(value);
    const formatted = rounded.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
    return `₸${formatted}`;
  }
}

/** Like currency2, but keeps 2 decimal places — for loyalty/bonus balances, which iiko tracks fractionally. */
@Pipe({ name: 'currencyDecimal', standalone: true })
export class CurrencyDecimalPipe implements PipeTransform {
  transform(value: number): string {
    const [whole, decimals] = value.toFixed(2).split('.');
    const formatted = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ' ');
    return `₸${formatted}.${decimals}`;
  }
}
