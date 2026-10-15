import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { YurtApiService } from 'shared-api';
import { ToastService } from 'shared-ui';
import { WorkerAccount } from 'shared-models';
import { AdminTranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-workers',
  standalone: true,
  imports: [CommonModule, FormsModule, AdminTranslatePipe],
  templateUrl: './workers.component.html',
})
export class WorkersComponent implements OnInit {
  private api = inject(YurtApiService);
  private toast = inject(ToastService);

  workers = signal<WorkerAccount[]>([]);
  loading = signal(true);

  showCreate = signal(false);
  createUsername = '';
  createPassword = '';
  createLoading = signal(false);

  editWorker = signal<WorkerAccount | null>(null);
  editUsername = '';
  editActive = true;
  editLoading = signal(false);

  resetWorker = signal<WorkerAccount | null>(null);
  resetPassword = '';
  resetLoading = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getWorkers().subscribe({
      next: (w) => { this.workers.set(w); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  openCreate(): void {
    this.createUsername = '';
    this.createPassword = '';
    this.showCreate.set(true);
  }

  submitCreate(): void {
    if (!this.createUsername || !this.createPassword) return;
    this.createLoading.set(true);
    this.api.createWorker({ username: this.createUsername, password: this.createPassword }).subscribe({
      next: (w) => {
        this.workers.update((list) => [...list, w]);
        this.showCreate.set(false);
        this.createLoading.set(false);
        this.toast.success('Worker account created.');
      },
      error: () => this.createLoading.set(false),
    });
  }

  openEdit(worker: WorkerAccount): void {
    this.editWorker.set(worker);
    this.editUsername = worker.username;
    this.editActive = worker.isActive;
  }

  submitEdit(): void {
    const w = this.editWorker();
    if (!w) return;
    this.editLoading.set(true);
    this.api.updateWorker(w.id, { username: this.editUsername, isActive: this.editActive }).subscribe({
      next: (updated) => {
        this.workers.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
        this.editWorker.set(null);
        this.editLoading.set(false);
        this.toast.success('Worker updated.');
      },
      error: () => this.editLoading.set(false),
    });
  }

  toggleActive(worker: WorkerAccount): void {
    this.api.setWorkerActive(worker.id, !worker.isActive).subscribe({
      next: (updated) => {
        this.workers.update((list) => list.map((x) => (x.id === updated.id ? updated : x)));
        this.toast.success(updated.isActive ? 'Worker activated.' : 'Worker deactivated.');
      },
      error: () => {},
    });
  }

  openReset(worker: WorkerAccount): void {
    this.resetWorker.set(worker);
    this.resetPassword = '';
  }

  submitReset(): void {
    const w = this.resetWorker();
    if (!w || !this.resetPassword) return;
    this.resetLoading.set(true);
    this.api.resetWorkerPassword(w.id, this.resetPassword).subscribe({
      next: () => {
        this.resetWorker.set(null);
        this.resetLoading.set(false);
        this.toast.success('Password reset.');
      },
      error: () => this.resetLoading.set(false),
    });
  }
}
