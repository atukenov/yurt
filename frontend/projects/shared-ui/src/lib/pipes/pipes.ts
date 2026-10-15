import { Pipe, PipeTransform } from '@angular/core';
import { OrderStatus, PaymentStatus } from 'shared-models';
import { BadgeVariant } from '../badge/badge.component';

@Pipe({ name: 'orderStatusLabel', standalone: true })
export class OrderStatusLabelPipe implements PipeTransform {
  transform(status: OrderStatus): string {
    const map: Record<OrderStatus, string> = {
      [OrderStatus.Created]: 'Order Placed',
      [OrderStatus.Accepted]: 'Accepted',
      [OrderStatus.Preparing]: 'Preparing',
      [OrderStatus.Ready]: 'Ready for Pickup',
      [OrderStatus.Completed]: 'Completed',
      [OrderStatus.Declined]: 'Declined',
    };
    return map[status] ?? status;
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

@Pipe({ name: 'paymentStatusLabel', standalone: true })
export class PaymentStatusLabelPipe implements PipeTransform {
  transform(status: PaymentStatus): string {
    const map: Record<PaymentStatus, string> = {
      [PaymentStatus.Unpaid]: 'Unpaid',
      [PaymentStatus.Pending]: 'Pending',
      [PaymentStatus.Paid]: 'Paid',
      [PaymentStatus.Failed]: 'Failed',
      [PaymentStatus.Expired]: 'Expired',
      [PaymentStatus.Refunded]: 'Refunded',
    };
    return map[status] ?? status;
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
