import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export interface AuthUser {
  accessToken: string;
  refreshToken: string;
  userId: string;
  displayName: string;
  userType: string;
  role?: string;
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
  get isAdmin(): boolean {
    return this.currentUser?.role === 'Admin';
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
