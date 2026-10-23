import { TestBed } from '@angular/core/testing';
import { OrderStatus, PaymentStatus } from 'shared-models';
import { UI_LANG } from '../locale/locale-format';
import { OrderStatusLabelPipe, PaymentStatusLabelPipe } from './pipes';

function pipesIn(lang: string) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({ providers: [{ provide: UI_LANG, useValue: () => lang }] });
  return TestBed.runInInjectionContext(() => ({ order: new OrderStatusLabelPipe(), payment: new PaymentStatusLabelPipe() }));
}

describe('status label pipes', () => {
  it('speak the app language', () => {
    expect(pipesIn('ru').order.transform(OrderStatus.Ready)).toBe('Готов к выдаче');
    expect(pipesIn('kk').order.transform(OrderStatus.Declined)).toBe('Қабылданбады');
    expect(pipesIn('ru').payment.transform(PaymentStatus.Paid)).toBe('Оплачен');
    expect(pipesIn('kk').payment.transform(PaymentStatus.Refunded)).toBe('Қайтарылды');
  });

  it('keep English as before', () => {
    expect(pipesIn('en').order.transform(OrderStatus.Created)).toBe('Order Placed');
    expect(pipesIn('en').payment.transform(PaymentStatus.Unpaid)).toBe('Unpaid');
  });
});
