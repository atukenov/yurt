import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface AuthUser {
  accessToken: string;
  refreshToken: string;
  userId: string;
  displayName: string;
  userType: string;
  role?: string;
  /** Key of the cafe this admin belongs to; sent as X-Tenant so one admin panel address serves every cafe. */
  cafe?: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuthStateService {
  private readonly TOKEN_KEY = 'yurt_token';
  private readonly REFRESH_TOKEN_KEY = 'yurt_refresh_token';
  private readonly USER_KEY = 'yurt_user';

  private _user$ = new BehaviorSubject<AuthUser | null>(this.loadFromStorage());
  readonly user$ = this._user$.asObservable();

  get currentUser(): AuthUser | null {
    return this._user$.getValue();
  }
  get token(): string | null {
    return this.currentUser?.accessToken ?? null;
  }
  get refreshToken(): string | null {
    return this.currentUser?.refreshToken ?? localStorage.getItem(this.REFRESH_TOKEN_KEY);
  }
  get isLoggedIn(): boolean {
    return !!this.currentUser;
  }
  /** Cafe admin, the cafe's CEO or the platform operator (each has every permission of the one before). */
  get isAdmin(): boolean {
    const role = this.currentUser?.role;
    return role === 'Admin' || role === 'Ceo' || role === 'SuperAdmin';
  }
  /** The cafe's owner: money screens (sales, billing, audit log) and payroll. Admins and workers do not see them. */
  get isCeo(): boolean {
    return this.currentUser?.role === 'Ceo';
  }
  get cafe(): string | null {
    return this.currentUser?.cafe ?? null;
  }
  get isSuperAdmin(): boolean {
    return this.currentUser?.role === 'SuperAdmin';
  }

  setUser(user: AuthUser): void {
    this._user$.next(user);
    localStorage.setItem(this.TOKEN_KEY, user.accessToken);
    localStorage.setItem(this.REFRESH_TOKEN_KEY, user.refreshToken);
    localStorage.setItem(this.USER_KEY, JSON.stringify(user));
  }

  updateTokens(accessToken: string, refreshToken: string): void {
    const user = this.currentUser;
    if (!user) return;
    const updated = { ...user, accessToken, refreshToken };
    this._user$.next(updated);
    localStorage.setItem(this.TOKEN_KEY, accessToken);
    localStorage.setItem(this.REFRESH_TOKEN_KEY, refreshToken);
    localStorage.setItem(this.USER_KEY, JSON.stringify(updated));
  }

  logout(): void {
    this._user$.next(null);
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
  }

  private loadFromStorage(): AuthUser | null {
    try {
      const raw = localStorage.getItem(this.USER_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }
}
