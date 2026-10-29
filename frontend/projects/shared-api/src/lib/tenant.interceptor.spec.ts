import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthStateService } from './auth-state.service';
import { TENANT_SLUG, tenantInterceptor } from './tenant.interceptor';

describe('tenantInterceptor', () => {
  function setup(appTenant: string | null, adminCafe: string | null) {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([tenantInterceptor])),
        provideHttpClientTesting(),
        { provide: TENANT_SLUG, useValue: appTenant },
      ],
    });
    if (adminCafe) {
      TestBed.inject(AuthStateService).setUser({
        accessToken: 't', refreshToken: 'r', userId: 'u', displayName: 'a', userType: 'admin', role: 'Admin', cafe: adminCafe,
      });
    }
    return { http: TestBed.inject(HttpClient), ctrl: TestBed.inject(HttpTestingController) };
  }

  afterEach(() => localStorage.clear());

  it('sends the signed-in admin\'s cafe', () => {
    const { http, ctrl } = setup(null, 'blue-cafe');
    http.get('/api/v1/admin/orders').subscribe();
    expect(ctrl.expectOne('/api/v1/admin/orders').request.headers.get('X-Tenant')).toBe('blue-cafe');
  });

  it('prefers the cafe a branded app is built for', () => {
    const { http, ctrl } = setup('green-cafe', 'blue-cafe');
    http.get('/api/v1/menu').subscribe();
    expect(ctrl.expectOne('/api/v1/menu').request.headers.get('X-Tenant')).toBe('green-cafe');
  });

  it('keeps a cafe the request already names', () => {
    const { http, ctrl } = setup('green-cafe', null);
    http.get('/api/v1/platform/brand', { headers: { 'X-Tenant': 'red-cafe' } }).subscribe();
    expect(ctrl.expectOne('/api/v1/platform/brand').request.headers.get('X-Tenant')).toBe('red-cafe');
  });

  it('sends nothing when no cafe is known', () => {
    const { http, ctrl } = setup(null, null);
    http.get('/api/v1/menu').subscribe();
    expect(ctrl.expectOne('/api/v1/menu').request.headers.has('X-Tenant')).toBe(false);
  });
});
