import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { ToastService } from 'shared-ui';
import { DiscountCode } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

@Component({
  selector: 'app-discount-codes',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe],
  templateUrl: './discount-codes.component.html',
})
export class DiscountCodesComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private confirmSvc = inject(ConfirmService);

  codes = signal<DiscountCode[]>([]);
  loading = signal(true);

  showForm = signal(false);
  editCode = signal<DiscountCode | null>(null);
  saving = signal(false);

  formCode = '';
  formTitle = '';
  formType: 'Percentage' | 'FixedAmount' = 'Percentage';
  formValue = 10;
  formMaxUses: number | null = null;
  formMinOrder: number | null = null;
  formStartsAt = '';
  formExpiresAt = '';
  formActive = true;

  readonly isEditing = computed(() => this.editCode() !== null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getAdminDiscountCodes().subscribe({
      next: (codes) => { this.codes.set(codes); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openCreate(): void {
    this.editCode.set(null);
    this.resetForm();
    this.showForm.set(true);
  }

  openEdit(code: DiscountCode): void {
    this.editCode.set(code);
    this.formCode = code.code;
    this.formTitle = code.title;
    this.formType = code.discountType;
    this.formValue = code.discountValue;
    this.formMaxUses = code.maxUses ?? null;
    this.formMinOrder = code.minOrderAmount ?? null;
    this.formStartsAt = code.startsAt ? code.startsAt.slice(0, 16) : '';
    this.formExpiresAt = code.expiresAt ? code.expiresAt.slice(0, 16) : '';
    this.formActive = code.isActive;
    this.showForm.set(true);
  }

  save(): void {
    if (!this.formCode.trim() || !this.formTitle.trim() || this.formValue <= 0) {
      this.toast.warning('Please fill in all required fields.');
      return;
    }
    this.saving.set(true);
    const payload = {
      code: this.formCode.trim().toUpperCase(),
      title: this.formTitle.trim(),
      discountType: this.formType,
      discountValue: this.formValue,
      maxUses: this.formMaxUses || undefined,
      minOrderAmount: this.formMinOrder || undefined,
      startsAt: this.formStartsAt || undefined,
      expiresAt: this.formExpiresAt || undefined,
      isActive: this.formActive,
    };

    const edit = this.editCode();
    const req$ = edit
      ? this.api.updateDiscountCode(edit.id, payload)
      : this.api.createDiscountCode(payload);

    req$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.toast.success(edit ? 'Discount code updated.' : 'Discount code created.');
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }

  async delete(code: DiscountCode): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Code', `Delete code "${code.code}"?`)) return;
    this.api.deleteDiscountCode(code.id).subscribe({
      next: () => { this.toast.success('Deleted.'); this.load(); },
      error: () => {},
    });
  }

  cancel(): void {
    this.showForm.set(false);
    this.resetForm();
  }

  isExpired(code: DiscountCode): boolean {
    return !!code.expiresAt && new Date(code.expiresAt) < new Date();
  }

  private resetForm(): void {
    this.formCode = '';
    this.formTitle = '';
    this.formType = 'Percentage';
    this.formValue = 10;
    this.formMaxUses = null;
    this.formMinOrder = null;
    this.formStartsAt = '';
    this.formExpiresAt = '';
    this.formActive = true;
  }
}
