import { HttpClient, HttpContext, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AcceptOrderRequest,
  AddGroupOrderItemRequest,
  AnalyticsResponse,
  ApplePayConfigResponse,
  AppRelease,
  AppConfig,
  IntegrationStatus,
  MaintenanceInfo,
  Cafe,
  CafeSecretStatus,
  CreateCafeRequest,
  BrandConfig,
  PlatformModule,
  UpdateModuleLicenseRequest,
  AppUpdateInfo,
  AuditLogEntry,
  AuthResponse,
  ConfirmApplePayRequest,
  CreateOrderRequest,
  CreatePaymentRequest,
  CustomerDetail,
  CustomerProfile,
  CustomerStats,
  CustomerSummary,
  DashboardData,
  DeclineOrderRequest,
  DiscountCode,
  GroupCart,
  IikoNomenclatureProduct,
  IikoPaymentType,
  IikoTerminalGroup,
  Location,
  LoyaltyBalance,
  LoyaltyHistory,
  MenuCategory,
  MenuItem,
  MenuTopping,
  Order,
  StockItem,
  StockAlerts,
  Station,
  StationRouting,
  CategoryRouting,
  Shift,
  ShiftReport,
  CashMovement,
  Ingredient,
  RecipeLine,
  OrderStatus,
  PaymentInvoiceResponse,
  PaymentStatusResponse,
  Promotion,
  RegistrationStartResponse,
  UpdatePaymentRequest,
  UpdateStatusRequest,
  UserReport,
  ValidateDiscountCodeResponse,
  WorkerAccount,
  SubscriptionInfo,
} from 'shared-models';

@Injectable({ providedIn: 'root' })
export class YurtApiService {
  private readonly http = inject(HttpClient);
  private baseUrl = '';

  configure(baseUrl: string): void {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  private get api(): string {
    return `${this.baseUrl}/api/v1`;
  }

  // ── App ────────────────────────────────────────────────────────────────────
  getAppUpdateInfo(): Observable<AppUpdateInfo> {
    return this.http.get<AppUpdateInfo>(`${this.api}/app/update-info`);
  }

  getAppConfig(): Observable<AppConfig> {
    return this.http.get<AppConfig>(`${this.api}/app/config`);
  }

  getAdminSubscription(): Observable<SubscriptionInfo> {
    return this.http.get<SubscriptionInfo>(`${this.api}/admin/subscription`);
  }

  // ── Platform operator tools (SuperAdmin only) ─────────────────────────────
  /** Slug of the cafe the operator is managing; null means the cafe of the host the page was opened from. */
  platformCafe: string | null = null;

  private cafeOptions(): { headers?: Record<string, string> } {
    return this.platformCafe ? { headers: { 'X-Tenant': this.platformCafe } } : {};
  }

  getPlatformModules(): Observable<PlatformModule[]> {
    return this.http.get<PlatformModule[]>(`${this.api}/platform/modules`, this.cafeOptions());
  }
  updatePlatformModule(key: string, req: UpdateModuleLicenseRequest): Observable<PlatformModule> {
    return this.http.put<PlatformModule>(
      `${this.api}/platform/modules/${key}`,
      req,
      this.cafeOptions(),
    );
  }
  getCafes(): Observable<Cafe[]> {
    return this.http.get<Cafe[]>(`${this.api}/platform/cafes`);
  }
  createCafe(req: CreateCafeRequest): Observable<Cafe> {
    return this.http.post<Cafe>(`${this.api}/platform/cafes`, req);
  }
  setCafeActive(id: string, isActive: boolean): Observable<Cafe> {
    return this.http.put<Cafe>(`${this.api}/platform/cafes/${id}/active`, { isActive });
  }
  getCafeSecrets(id: string): Observable<CafeSecretStatus[]> {
    return this.http.get<CafeSecretStatus[]>(`${this.api}/platform/cafes/${id}/secrets`);
  }
  setCafeSecret(id: string, key: string, value: string): Observable<CafeSecretStatus> {
    return this.http.put<CafeSecretStatus>(
      `${this.api}/platform/cafes/${id}/secrets/${encodeURIComponent(key)}`,
      { value },
    );
  }
  clearCafeSecret(id: string, key: string): Observable<CafeSecretStatus> {
    return this.http.delete<CafeSecretStatus>(
      `${this.api}/platform/cafes/${id}/secrets/${encodeURIComponent(key)}`,
    );
  }
  getMaintenance(): Observable<MaintenanceInfo> {
    return this.http.get<MaintenanceInfo>(`${this.api}/platform/maintenance`, this.cafeOptions());
  }
  setMaintenance(req: MaintenanceInfo): Observable<MaintenanceInfo> {
    return this.http.put<MaintenanceInfo>(
      `${this.api}/platform/maintenance`,
      req,
      this.cafeOptions(),
    );
  }
  getIntegrationStatus(): Observable<IntegrationStatus[]> {
    return this.http.get<IntegrationStatus[]>(`${this.api}/platform/integrations`);
  }

  getBrand(): Observable<BrandConfig> {
    return this.http.get<BrandConfig>(`${this.api}/platform/brand`, this.cafeOptions());
  }
  setBrand(req: BrandConfig): Observable<BrandConfig> {
    return this.http.put<BrandConfig>(`${this.api}/platform/brand`, req, this.cafeOptions());
  }

  // App releases (what's-new popup / forced update) - operator tool under /platform
  getAdminAppReleases(): Observable<AppRelease[]> {
    return this.http.get<AppRelease[]>(`${this.api}/platform/app-releases`);
  }
  createAppRelease(req: Partial<AppRelease>): Observable<AppRelease> {
    return this.http.post<AppRelease>(`${this.api}/platform/app-releases`, req);
  }
  updateAppRelease(id: string, req: Partial<AppRelease>): Observable<AppRelease> {
    return this.http.put<AppRelease>(`${this.api}/platform/app-releases/${id}`, req);
  }
  deleteAppRelease(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/platform/app-releases/${id}`);
  }

  // ── Auth ───────────────────────────────────────────────────────────────────
  registerStart(
    mobileNumber: string,
    pin4: string,
    firstName: string = '',
    lastName: string = '',
  ): Observable<RegistrationStartResponse> {
    return this.http.post<RegistrationStartResponse>(`${this.api}/auth/register/start`, {
      mobileNumber,
      pin4,
      firstName,
      lastName,
    });
  }

  registerVerify(mobileNumber: string, code: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/register/verify`, {
      mobileNumber,
      code,
    });
  }

  login(mobileNumber: string, pin4: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/login`, { mobileNumber, pin4 });
  }

  me(): Observable<CustomerProfile> {
    return this.http.get<CustomerProfile>(`${this.api}/auth/me`);
  }

  updateProfile(data: {
    firstName: string;
    lastName: string;
    dateOfBirth?: string | null;
  }): Observable<CustomerProfile> {
    return this.http.put<CustomerProfile>(`${this.api}/auth/me`, data);
  }

  changeMobileNumberStart(mobileNumber: string): Observable<RegistrationStartResponse> {
    return this.http.post<RegistrationStartResponse>(`${this.api}/auth/me/phone/start`, {
      mobileNumber,
    });
  }

  changeMobileNumberVerify(mobileNumber: string, code: string): Observable<CustomerProfile> {
    return this.http.post<CustomerProfile>(`${this.api}/auth/me/phone/verify`, {
      mobileNumber,
      code,
    });
  }

  refreshToken(refreshToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/auth/refresh`, { refreshToken });
  }

  adminRefreshToken(refreshToken: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/admin/auth/refresh`, { refreshToken });
  }

  logout(refreshToken: string): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/logout`, { refreshToken });
  }

  adminLogout(refreshToken: string): Observable<void> {
    return this.http.post<void>(`${this.api}/admin/auth/logout`, { refreshToken });
  }

  adminLogin(username: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/admin/auth/login`, {
      username,
      password,
    });
  }

  adminChangePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.api}/admin/auth/change-password`, {
      currentPassword,
      newPassword,
    });
  }

  // ── Locations ──────────────────────────────────────────────────────────────
  getLocations(lang?: string): Observable<Location[]> {
    const params = lang ? new HttpParams().set('lang', lang) : undefined;
    return this.http.get<Location[]>(`${this.api}/locations`, { params });
  }

  getAdminLocations(): Observable<Location[]> {
    return this.http.get<Location[]>(`${this.api}/admin/locations`);
  }

  createLocation(data: Partial<Location>): Observable<Location> {
    return this.http.post<Location>(`${this.api}/admin/locations`, data);
  }

  updateLocation(id: string, data: Partial<Location>): Observable<Location> {
    return this.http.put<Location>(`${this.api}/admin/locations/${id}`, data);
  }

  deleteLocation(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/locations/${id}`);
  }

  // ── Menu ───────────────────────────────────────────────────────────────────
  getCategories(lang?: string, locationId?: string): Observable<MenuCategory[]> {
    let params = new HttpParams();
    if (lang) params = params.set('lang', lang);
    if (locationId) params = params.set('locationId', locationId);
    return this.http.get<MenuCategory[]>(`${this.api}/menu/categories`, { params });
  }

  getMenuItems(
    categoryId?: string,
    search?: string,
    lang?: string,
    locationId?: string,
  ): Observable<MenuItem[]> {
    let params = new HttpParams();
    if (categoryId) params = params.set('categoryId', categoryId);
    if (search) params = params.set('search', search);
    if (lang) params = params.set('lang', lang);
    if (locationId) params = params.set('locationId', locationId);
    return this.http.get<MenuItem[]>(`${this.api}/menu`, { params });
  }

  getMenuItem(id: string, lang?: string, locationId?: string): Observable<MenuItem> {
    let params = new HttpParams();
    if (lang) params = params.set('lang', lang);
    if (locationId) params = params.set('locationId', locationId);
    return this.http.get<MenuItem>(`${this.api}/menu/items/${id}`, { params });
  }

  // Admin menu
  adminGetMenuItems(): Observable<MenuItem[]> {
    return this.http.get<MenuItem[]>(`${this.api}/admin/menu/items`);
  }

  adminCreateMenuItem(data: Partial<MenuItem>): Observable<MenuItem> {
    return this.http.post<MenuItem>(`${this.api}/admin/menu/items`, data);
  }

  adminUpdateMenuItem(id: string, data: Partial<MenuItem>): Observable<MenuItem> {
    return this.http.put<MenuItem>(`${this.api}/admin/menu/items/${id}`, data);
  }

  adminDeleteMenuItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/menu/items/${id}`);
  }

  adminReorderMenuItems(categoryId: string, orderedIds: string[]): Observable<void> {
    return this.http.patch<void>(`${this.api}/admin/menu/items/reorder`, {
      categoryId,
      orderedIds,
    });
  }

  adminGetCategories(): Observable<MenuCategory[]> {
    return this.http.get<MenuCategory[]>(`${this.api}/admin/menu/categories`);
  }

  adminCreateCategory(data: Partial<MenuCategory>): Observable<MenuCategory> {
    return this.http.post<MenuCategory>(`${this.api}/admin/menu/categories`, data);
  }

  adminUpdateCategory(id: string, data: Partial<MenuCategory>): Observable<MenuCategory> {
    return this.http.put<MenuCategory>(`${this.api}/admin/menu/categories/${id}`, data);
  }

  adminDeleteCategory(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/menu/categories/${id}`);
  }

  adminReorderCategories(orderedIds: string[]): Observable<void> {
    return this.http.patch<void>(`${this.api}/admin/menu/categories/reorder`, { orderedIds });
  }

  adminGetToppings(): Observable<MenuTopping[]> {
    return this.http.get<MenuTopping[]>(`${this.api}/admin/menu/toppings`);
  }

  adminCreateTopping(data: Partial<MenuTopping>): Observable<MenuTopping> {
    return this.http.post<MenuTopping>(`${this.api}/admin/menu/toppings`, data);
  }

  adminUpdateTopping(id: string, data: Partial<MenuTopping>): Observable<MenuTopping> {
    return this.http.put<MenuTopping>(`${this.api}/admin/menu/toppings/${id}`, data);
  }

  adminDeleteTopping(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/menu/toppings/${id}`);
  }

  getToppings(categoryId?: string, lang?: string): Observable<MenuTopping[]> {
    let params = new HttpParams();
    if (categoryId) params = params.set('categoryId', categoryId);
    if (lang) params = params.set('lang', lang);
    return this.http.get<MenuTopping[]>(`${this.api}/menu/toppings`, { params });
  }

  // ── Favorites ──────────────────────────────────────────────────────────────
  getFavorites(lang?: string): Observable<MenuItem[]> {
    const params = lang ? new HttpParams().set('lang', lang) : undefined;
    return this.http.get<MenuItem[]>(`${this.api}/favorites`, { params });
  }

  addFavorite(menuItemId: string): Observable<void> {
    return this.http.post<void>(`${this.api}/favorites/${menuItemId}`, {});
  }

  removeFavorite(menuItemId: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/favorites/${menuItemId}`);
  }

  // ── Orders ─────────────────────────────────────────────────────────────────
  createOrder(request: CreateOrderRequest, idempotencyKey?: string): Observable<Order> {
    const headers = idempotencyKey ? { 'Idempotency-Key': idempotencyKey } : undefined;
    return this.http.post<Order>(`${this.api}/orders`, request, { headers });
  }

  getActiveOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.api}/orders/active`);
  }

  createPayment(request: CreatePaymentRequest): Observable<PaymentInvoiceResponse> {
    return this.http.post<PaymentInvoiceResponse>(`${this.api}/payments/create`, request);
  }

  getPaymentStatus(invoiceId: string): Observable<PaymentStatusResponse> {
    return this.http.get<PaymentStatusResponse>(`${this.api}/payments/status/${invoiceId}`);
  }

  getApplePayConfig(): Observable<ApplePayConfigResponse> {
    return this.http.get<ApplePayConfigResponse>(`${this.api}/payments/apple-pay/config`);
  }

  confirmApplePay(request: ConfirmApplePayRequest): Observable<PaymentStatusResponse> {
    return this.http.post<PaymentStatusResponse>(`${this.api}/payments/apple-pay/confirm`, request);
  }

  getOrderHistory(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.api}/orders/history`);
  }

  getDeclinedOrders(): Observable<Order[]> {
    return this.http.get<Order[]>(`${this.api}/orders/declined`);
  }

  getOrder(id: string, context?: HttpContext): Observable<Order> {
    return this.http.get<Order>(`${this.api}/orders/${id}`, context && { context });
  }

  cancelOrder(id: string): Observable<Order> {
    return this.http.post<Order>(`${this.api}/orders/${id}/cancel`, {});
  }

  rateOrder(id: string, rating: number, comment?: string): Observable<Order> {
    return this.http.post<Order>(`${this.api}/orders/${id}/rating`, { rating, comment });
  }

  // Admin orders
  getAdminOrders(
    status?: OrderStatus,
    locationId?: string,
    search?: string,
    page = 1,
    pageSize = 100,
  ): Observable<{ total: number; page: number; pageSize: number; items: Order[] }> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) params = params.set('status', status);
    if (locationId) params = params.set('locationId', locationId);
    if (search) params = params.set('search', search);
    return this.http.get<{ total: number; page: number; pageSize: number; items: Order[] }>(
      `${this.api}/admin/orders`,
      { params },
    );
  }

  acceptOrder(id: string, req: AcceptOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/admin/orders/${id}/accept`, req);
  }

  declineOrder(id: string, req: DeclineOrderRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/admin/orders/${id}/decline`, req);
  }

  getAdminStock(locationId: string): Observable<StockItem[]> {
    return this.http.get<StockItem[]>(`${this.api}/admin/stock`, { params: { locationId } });
  }

  setAdminStock(
    menuItemId: string,
    locationId: string,
    body: { quantity: number | null; lowStockAt: number | null },
  ): Observable<StockItem> {
    return this.http.put<StockItem>(`${this.api}/admin/stock/${menuItemId}/${locationId}`, body);
  }

  /** Null when no shift is open at the branch (the body is empty). */
  getCurrentShift(locationId: string): Observable<Shift | null> {
    return this.http.get<Shift | null>(`${this.api}/admin/shifts/current`, {
      params: { locationId },
    });
  }

  getShifts(locationId: string): Observable<Shift[]> {
    return this.http.get<Shift[]>(`${this.api}/admin/shifts`, { params: { locationId } });
  }

  openShift(locationId: string, openingCash: number): Observable<Shift> {
    return this.http.post<Shift>(`${this.api}/admin/shifts/open`, { locationId, openingCash });
  }

  addCashMovement(
    shiftId: string,
    body: { amount: number; isOut: boolean; reason: string },
  ): Observable<CashMovement> {
    return this.http.post<CashMovement>(`${this.api}/admin/shifts/${shiftId}/cash`, body);
  }

  closeShift(
    shiftId: string,
    body: { countedCash: number; note?: string },
  ): Observable<ShiftReport> {
    return this.http.post<ShiftReport>(`${this.api}/admin/shifts/${shiftId}/close`, body);
  }

  getShiftReport(shiftId: string): Observable<ShiftReport> {
    return this.http.get<ShiftReport>(`${this.api}/admin/shifts/${shiftId}/report`);
  }

  getIngredients(locationId: string): Observable<Ingredient[]> {
    return this.http.get<Ingredient[]>(`${this.api}/admin/ingredients`, { params: { locationId } });
  }

  createIngredient(body: { name: string; unit: string }): Observable<Ingredient> {
    return this.http.post<Ingredient>(`${this.api}/admin/ingredients`, body);
  }

  deleteIngredient(id: string): Observable<boolean> {
    return this.http.delete<boolean>(`${this.api}/admin/ingredients/${id}`);
  }

  setIngredientStock(
    id: string,
    locationId: string,
    body: { quantity: number | null; lowStockAt: number | null },
  ): Observable<Ingredient> {
    return this.http.put<Ingredient>(
      `${this.api}/admin/ingredients/${id}/stock/${locationId}`,
      body,
    );
  }

  /** What is running low or out, at one branch or (with no branch) at all of them. */
  getStockAlerts(locationId?: string): Observable<StockAlerts> {
    return this.http.get<StockAlerts>(`${this.api}/admin/stock/alerts`, {
      params: locationId ? { locationId } : {},
    });
  }

  getStationRouting(): Observable<StationRouting> {
    return this.http.get<StationRouting>(`${this.api}/admin/stations/routing`);
  }

  createStation(name: string): Observable<Station> {
    return this.http.post<Station>(`${this.api}/admin/stations`, { name });
  }

  deleteStation(id: string): Observable<boolean> {
    return this.http.delete<boolean>(`${this.api}/admin/stations/${id}`);
  }

  setCategoryStation(categoryId: string, stationId: string | null): Observable<CategoryRouting> {
    return this.http.put<CategoryRouting>(`${this.api}/admin/stations/categories/${categoryId}`, {
      stationId,
    });
  }

  getToppingUses(toppingId: string): Observable<RecipeLine[]> {
    return this.http.get<RecipeLine[]>(`${this.api}/admin/ingredients/toppings/${toppingId}`);
  }

  saveToppingUses(toppingId: string, lines: RecipeLine[]): Observable<RecipeLine[]> {
    const body = {
      lines: lines.map((l) => ({ ingredientId: l.ingredientId, quantity: l.quantity })),
    };
    return this.http.put<RecipeLine[]>(`${this.api}/admin/ingredients/toppings/${toppingId}`, body);
  }

  getRecipe(menuItemId: string): Observable<RecipeLine[]> {
    return this.http.get<RecipeLine[]>(`${this.api}/admin/ingredients/recipes/${menuItemId}`);
  }

  saveRecipe(menuItemId: string, lines: RecipeLine[]): Observable<RecipeLine[]> {
    const body = {
      lines: lines.map((l) => ({
        ingredientId: l.ingredientId,
        variantId: l.variantId,
        quantity: l.quantity,
      })),
    };
    return this.http.put<RecipeLine[]>(`${this.api}/admin/ingredients/recipes/${menuItemId}`, body);
  }

  updateOrderStatus(id: string, req: UpdateStatusRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/admin/orders/${id}/status`, req);
  }

  updateOrderPayment(id: string, req: UpdatePaymentRequest): Observable<Order> {
    return this.http.post<Order>(`${this.api}/admin/orders/${id}/payment`, req);
  }

  // ── Dashboard ──────────────────────────────────────────────────────────────
  getDashboard(): Observable<DashboardData> {
    return this.http.get<DashboardData>(`${this.api}/admin/dashboard`);
  }

  // ── Customers (admin) ──────────────────────────────────────────────────────
  getAdminCustomers(
    phone?: string,
    page = 1,
    pageSize = 50,
  ): Observable<{ total: number; page: number; pageSize: number; items: CustomerSummary[] }> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (phone) params = params.set('phone', phone);
    return this.http.get<{
      total: number;
      page: number;
      pageSize: number;
      items: CustomerSummary[];
    }>(`${this.api}/admin/customers`, { params });
  }

  getAdminCustomer(id: string): Observable<CustomerDetail> {
    return this.http.get<CustomerDetail>(`${this.api}/admin/customers/${id}`);
  }

  setCustomerActive(id: string, isActive: boolean): Observable<CustomerDetail> {
    return this.http.patch<CustomerDetail>(`${this.api}/admin/customers/${id}/active`, {
      isActive,
    });
  }

  // ── Workers ────────────────────────────────────────────────────────────────
  getWorkers(): Observable<WorkerAccount[]> {
    return this.http.get<WorkerAccount[]>(`${this.api}/admin/workers`);
  }

  createWorker(data: { username: string; password: string }): Observable<WorkerAccount> {
    return this.http.post<WorkerAccount>(`${this.api}/admin/workers`, data);
  }

  updateWorker(
    id: string,
    data: { username: string; isActive: boolean },
  ): Observable<WorkerAccount> {
    return this.http.put<WorkerAccount>(`${this.api}/admin/workers/${id}`, data);
  }

  setWorkerActive(id: string, isActive: boolean): Observable<WorkerAccount> {
    return this.http.patch<WorkerAccount>(`${this.api}/admin/workers/${id}/active`, { isActive });
  }

  resetWorkerPassword(id: string, newPassword: string): Observable<WorkerAccount> {
    return this.http.post<WorkerAccount>(`${this.api}/admin/workers/${id}/reset-password`, {
      newPassword,
    });
  }

  // ── Customer account ────────────────────────────────────────────────────────
  changePin(currentPin: string, newPin: string): Observable<void> {
    return this.http.put<void>(`${this.api}/auth/pin`, { currentPin, newPin });
  }

  deleteAccount(): Observable<void> {
    return this.http.delete<void>(`${this.api}/auth/me`);
  }

  registerDeviceToken(token: string, platform: 'ios' | 'android'): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/device-tokens`, { token, platform });
  }

  unregisterDeviceToken(token: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/auth/device-tokens/${encodeURIComponent(token)}`);
  }

  getCustomerStats(): Observable<CustomerStats> {
    return this.http.get<CustomerStats>(`${this.api}/auth/me/stats`);
  }

  getLoyaltyBalance(): Observable<LoyaltyBalance> {
    return this.http.get<LoyaltyBalance>(`${this.api}/loyalty/me`);
  }

  getLoyaltyTransactions(): Observable<LoyaltyHistory> {
    return this.http.get<LoyaltyHistory>(`${this.api}/loyalty/transactions`);
  }

  getAdminCustomerLoyalty(customerId: string): Observable<LoyaltyBalance> {
    return this.http.get<LoyaltyBalance>(`${this.api}/admin/customers/${customerId}/loyalty`);
  }

  // ── Analytics ───────────────────────────────────────────────────────────────
  getAnalytics(period: string, locationId?: string): Observable<AnalyticsResponse> {
    let params = new HttpParams().set('period', period);
    if (locationId) params = params.set('locationId', locationId);
    return this.http.get<AnalyticsResponse>(`${this.api}/admin/analytics`, { params });
  }

  // ── Promotions ──────────────────────────────────────────────────────────────
  getActivePromotions(): Observable<Promotion[]> {
    return this.http.get<Promotion[]>(`${this.api}/promotions/active`);
  }

  getAdminPromotions(): Observable<Promotion[]> {
    return this.http.get<Promotion[]>(`${this.api}/admin/promotions`);
  }

  createPromotion(data: Partial<Promotion>): Observable<Promotion> {
    return this.http.post<Promotion>(`${this.api}/admin/promotions`, data);
  }

  updatePromotion(id: string, data: Partial<Promotion>): Observable<Promotion> {
    return this.http.put<Promotion>(`${this.api}/admin/promotions/${id}`, data);
  }

  deletePromotion(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/promotions/${id}`);
  }

  // ── Discount Codes ──────────────────────────────────────────────────────────
  getAdminDiscountCodes(): Observable<DiscountCode[]> {
    return this.http.get<DiscountCode[]>(`${this.api}/admin/discount-codes`);
  }

  createDiscountCode(data: Partial<DiscountCode>): Observable<DiscountCode> {
    return this.http.post<DiscountCode>(`${this.api}/admin/discount-codes`, data);
  }

  updateDiscountCode(id: string, data: Partial<DiscountCode>): Observable<DiscountCode> {
    return this.http.put<DiscountCode>(`${this.api}/admin/discount-codes/${id}`, data);
  }

  deleteDiscountCode(id: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/admin/discount-codes/${id}`);
  }

  validateDiscountCode(code: string, subtotal: number): Observable<ValidateDiscountCodeResponse> {
    return this.http.post<ValidateDiscountCodeResponse>(`${this.api}/discount-codes/validate`, {
      code,
      subtotal,
    });
  }

  // ── Audit Log ───────────────────────────────────────────────────────────────
  getAuditLog(params: {
    entityType?: string;
    adminId?: string;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }): Observable<{ total: number; page: number; pageSize: number; items: AuditLogEntry[] }> {
    let httpParams = new HttpParams();
    if (params.entityType) httpParams = httpParams.set('entityType', params.entityType);
    if (params.adminId) httpParams = httpParams.set('adminId', params.adminId);
    if (params.from) httpParams = httpParams.set('from', params.from);
    if (params.to) httpParams = httpParams.set('to', params.to);
    if (params.page) httpParams = httpParams.set('page', params.page);
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize);
    return this.http.get<{ total: number; page: number; pageSize: number; items: AuditLogEntry[] }>(
      `${this.api}/admin/audit-log`,
      { params: httpParams },
    );
  }

  // ── Group Orders ────────────────────────────────────────────────────────────
  createGroupOrder(locationId: string): Observable<GroupCart> {
    return this.http.post<GroupCart>(`${this.api}/group-orders`, { locationId });
  }

  joinGroupOrder(code: string): Observable<GroupCart> {
    return this.http.post<GroupCart>(`${this.api}/group-orders/join`, { code });
  }

  getGroupOrder(id: string): Observable<GroupCart> {
    return this.http.get<GroupCart>(`${this.api}/group-orders/${id}`);
  }

  addGroupOrderItem(id: string, req: AddGroupOrderItemRequest): Observable<GroupCart> {
    return this.http.post<GroupCart>(`${this.api}/group-orders/${id}/items`, req);
  }

  removeGroupOrderItem(id: string, itemId: string): Observable<GroupCart> {
    return this.http.delete<GroupCart>(`${this.api}/group-orders/${id}/items/${itemId}`);
  }

  checkoutGroupOrder(id: string): Observable<Order> {
    return this.http.post<Order>(`${this.api}/group-orders/${id}/checkout`, {});
  }

  submitReport(text: string): Observable<void> {
    return this.http.post<void>(`${this.api}/reports`, { text });
  }

  getAdminReports(resolved = false): Observable<UserReport[]> {
    return this.http.get<UserReport[]>(`${this.api}/admin/reports`, {
      params: { resolved: String(resolved) },
    });
  }

  resolveReport(id: string): Observable<void> {
    return this.http.post<void>(`${this.api}/admin/reports/${id}/resolve`, {});
  }

  // ── iiko (admin reference lookups for the menu-mapping / location pickers) ───
  getIikoNomenclature(): Observable<IikoNomenclatureProduct[]> {
    return this.http.get<IikoNomenclatureProduct[]>(`${this.api}/admin/iiko/nomenclature`);
  }

  getIikoPaymentTypes(): Observable<IikoPaymentType[]> {
    return this.http.get<IikoPaymentType[]>(`${this.api}/admin/iiko/payment-types`);
  }

  getIikoTerminalGroups(): Observable<IikoTerminalGroup[]> {
    return this.http.get<IikoTerminalGroup[]>(`${this.api}/admin/iiko/terminal-groups`);
  }
}
