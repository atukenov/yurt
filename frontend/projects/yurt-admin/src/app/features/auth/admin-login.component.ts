import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { YurtApiService, AuthStateService } from 'shared-api';
import { ButtonComponent, ToastService } from 'shared-ui';

@Component({
  selector: 'app-admin-login',
  standalone: true,
  imports: [CommonModule, FormsModule, ButtonComponent],
  templateUrl: './admin-login.component.html',
  styleUrl: './admin-login.component.css',
})
export class AdminLoginComponent {
  private api = inject(YurtApiService);
  private auth = inject(AuthStateService);
  private router = inject(Router);
  private toast = inject(ToastService);

  username = '';
  password = '';
  loading = signal(false);
  error = signal('');

  login(): void {
    if (!this.username || !this.password) return;
    this.loading.set(true);
    this.error.set('');
    this.api.adminLogin(this.username, this.password).subscribe({
      next: (res) => {
        this.auth.setUser({
          accessToken: res.accessToken,
          refreshToken: res.refreshToken,
          userId: res.userId,
          displayName: res.displayName,
          userType: 'admin',
          role: res.role,
        });
        if (res.mustChangePassword) {
          this.router.navigate(['/auth/change-password']);
        } else {
          this.router.navigate(['/dashboard']);
        }
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Invalid username or password.');
      },
    });
  }
}