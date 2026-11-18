import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ModuleService, YurtApiService } from 'shared-api';
import { MenuItem } from 'shared-models';
import { ToastService } from 'shared-ui';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';
import { MenuManagementComponent } from './menu-management.component';

const dish = (id: string, price: number): MenuItem => ({
  id, categoryId: 'c', categoryName: 'C', name: id, description: '', price, isAvailable: true, isNew: false,
  variants: [{ id: 'v', label: 'Big', price: price + 100, sortOrder: 0, isDefault: true }],
  locationPrices: [{ locationId: 'l1', price: price + 50 }],
});

describe('menu bulk price change', () => {
  it('asks to confirm with an Apply button, then saves every ticked dish with new prices', async () => {
    const saved: { id: string; body: Partial<MenuItem> }[] = [];
    const confirmCalls: (string | undefined)[] = [];
    TestBed.configureTestingModule({
      providers: [
        { provide: YurtApiService, useValue: { adminUpdateMenuItem: (id: string, body: Partial<MenuItem>) => { saved.push({ id, body }); return of({ ...dish(id, body.price!), ...body } as MenuItem); } } },
        { provide: ModuleService, useValue: { isEnabled: () => true } },
        { provide: ToastService, useValue: { success: () => {}, error: () => {} } },
        { provide: ConfirmService, useValue: { confirm: (_t: string, _m: string, label?: string) => { confirmCalls.push(label); return Promise.resolve(true); } } },
      ],
    });
    TestBed.overrideComponent(MenuManagementComponent, { set: { template: '', imports: [] } });
    const cmp = TestBed.createComponent(MenuManagementComponent).componentInstance;
    cmp.items.set([dish('a', 1000), dish('b', 500)]);
    cmp.selectedIds.set(['a', 'b']);
    cmp.bulkPercent.set(10);
    await cmp.bulkChangePrice();
    expect(confirmCalls[0]).toBeTruthy();
    expect(saved.map((s) => s.body.price)).toEqual([1100, 550]);
    expect(saved[0].body.variants?.[0].price).toBe(1210);
    expect(saved[0].body.locationPrices?.[0].price).toBe(1155);
    expect(cmp.items().map((i) => i.price)).toEqual([1100, 550]);
  });
});
