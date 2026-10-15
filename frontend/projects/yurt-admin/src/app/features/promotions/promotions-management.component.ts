import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { Promotion } from 'shared-models';
import { ToastService } from 'shared-ui';
import { AdminTranslatePipe } from '../../core/translate.pipe';
import { ConfirmService } from '../../shared/confirm-dialog/confirm.service';

interface PromotionForm {
  id?: string;
  title: string;
  description: string;
  imageUrl: string;
  buttonLabel: string;
  buttonUrl: string;
  isActive: boolean;
  expiresAt: string;
}

@Component({
  selector: 'app-promotions-management',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe],
  templateUrl: './promotions-management.component.html',
  styleUrl: './promotions-management.component.css',
})
export class PromotionsManagementComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);
  private confirmSvc = inject(ConfirmService);

  promotions = signal<Promotion[]>([]);
  loading = signal(true);
  saving = signal(false);
  showDialog = signal(false);
  form = signal<PromotionForm>({
    title: '',
    description: '',
    imageUrl: '',
    buttonLabel: '',
    buttonUrl: '',
    isActive: true,
    expiresAt: '',
  });

  ngOnInit(): void {
    this.api.getAdminPromotions().subscribe({
      next: (promos) => {
        this.promotions.set(promos);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  openDialog(promo?: Promotion): void {
    this.form.set({
      id: promo?.id,
      title: promo?.title ?? '',
      description: promo?.description ?? '',
      imageUrl: promo?.imageUrl ?? '',
      buttonLabel: promo?.buttonLabel ?? '',
      buttonUrl: promo?.buttonUrl ?? '',
      isActive: promo?.isActive ?? true,
      expiresAt: promo?.expiresAt ? promo.expiresAt.slice(0, 16) : '',
    });
    this.showDialog.set(true);
  }

  patch(field: keyof PromotionForm, value: unknown): void {
    this.form.update((f) => ({ ...f, [field]: value }));
  }

  save(): void {
    const f = this.form();
    if (!f.title) {
      this.toast.error('Title is required');
      return;
    }
    this.saving.set(true);

    const payload: Partial<Promotion> = {
      title: f.title,
      description: f.description,
      imageUrl: f.imageUrl || undefined,
      buttonLabel: f.buttonLabel || undefined,
      buttonUrl: f.buttonUrl || undefined,
      isActive: f.isActive,
      expiresAt: f.expiresAt ? new Date(f.expiresAt).toISOString() : undefined,
    };

    const obs = f.id ? this.api.updatePromotion(f.id, payload) : this.api.createPromotion(payload);

    obs.subscribe({
      next: (promo) => {
        if (f.id) {
          this.promotions.update((list) => list.map((p) => (p.id === promo.id ? promo : p)));
        } else {
          this.promotions.update((list) => [promo, ...list]);
        }
        this.saving.set(false);
        this.showDialog.set(false);
        this.toast.success('Promotion saved');
      },
      error: () => this.saving.set(false),
    });
  }

  async deletePromotion(promo: Promotion): Promise<void> {
    if (!await this.confirmSvc.confirm('Delete Promotion', `Delete "${promo.title}"?`)) return;
    this.api.deletePromotion(promo.id).subscribe({
      next: () => {
        this.promotions.update((list) => list.filter((p) => p.id !== promo.id));
        this.toast.success('Promotion deleted');
      },
      error: () => {},
    });
  }

  isExpired(promo: Promotion): boolean {
    return !!promo.expiresAt && new Date(promo.expiresAt) < new Date();
  }
}
