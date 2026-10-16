// ── Enums ─────────────────────────────────────────────────────────────────────

export enum OrderStatus {
  Created = 'Created',
  Accepted = 'Accepted',
  Preparing = 'Preparing',
  Ready = 'Ready',
  Completed = 'Completed',
  Declined = 'Declined',
}

export enum PaymentStatus {
  Unpaid = 'Unpaid',
  Pending = 'Pending',
  Paid = 'Paid',
  Failed = 'Failed',
  Expired = 'Expired',
  Refunded = 'Refunded',
}

export enum PaymentMethod {
  Cash = 'Cash',
  Card = 'Card',
  Other = 'Other',
  KaspiBank = 'KaspiBank',
  HalykBank = 'HalykBank',
  FreedomBank = 'FreedomBank',
  ApplePay = 'ApplePay',
}

export enum PaymentProvider {
  KaspiSandbox = 'KaspiSandbox',
  AiPay = 'AiPay',
  PayBot = 'PayBot',
  Halyk = 'Halyk',
  FreedomPay = 'FreedomPay',
  Stripe = 'Stripe',
  PayPal = 'PayPal',
}

export enum Currency {
  KZT = 'KZT',
  USD = 'USD',
  EUR = 'EUR',
}

export enum SandboxPaymentBehavior {
  Default = 'Default',
  Success = 'Success',
  Failure = 'Failure',
  Expired = 'Expired',
  DuplicateWebhook = 'DuplicateWebhook',
  NetworkFailure = 'NetworkFailure',
}

// ── Auth ──────────────────────────────────────────────────────────────────────

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  userType: string;
  userId: string;
  displayName: string;
  role?: string;
  mustChangePassword?: boolean;
}

export interface RegistrationStartResponse {
  mobileNumber: string;
  expiresAt: string;
  devCode?: string | null;
  /** false when the server has phone verification switched off: no code step follows. */
  verificationRequired?: boolean;
  /** Tokens, present when registration completed without a code. */
  auth?: AuthResponse | null;
}

export interface CustomerProfile {
  id: string;
  mobileNumber: string;
  firstName: string;
  lastName: string;
  createdAt: string;
  dateOfBirth?: string | null;
}

// ── Location ──────────────────────────────────────────────────────────────────

export interface Location {
  id: string;
  name: string;
  address: string;
  workingHours: string;
  contactPhone: string;
  isActive: boolean;
  iikoTerminalGroupId?: string | null;
  twoGisUrl?: string | null;
}

// ── Menu ──────────────────────────────────────────────────────────────────────

export interface MenuCategory {
  id: string;
  name: string;
  nameRu?: string;
  nameKk?: string;
  sortOrder: number;
}

export interface MenuTopping {
  id: string;
  name: string;
  nameRu?: string;
  nameKk?: string;
  price: number;
  isAvailable: boolean;
  categoryIds: string[];
  group?: string | null;
}

export interface MenuItemVariant {
  id?: string;
  label: string;
  labelRu?: string;
  labelKk?: string;
  price: number;
  sortOrder: number;
  isDefault: boolean;
  iikoProductSizeId?: string | null;
}

export interface MenuItem {
  id: string;
  categoryId: string;
  categoryName: string;
  name: string;
  nameEn?: string;
  nameRu?: string;
  nameKk?: string;
  description: string;
  descriptionRu?: string;
  descriptionKk?: string;
  price: number;
  isAvailable: boolean;
  isNew: boolean;
  imageUrl?: string;
  sortOrder?: number;
  createdAt?: string;
  locationIds?: string[];
  availableToppings?: MenuTopping[];
  variants?: MenuItemVariant[];
  iikoProductId?: string | null;
  iikoProductSizeId?: string | null;
}

// ── iiko reference lookups (admin mapping UI) ──────────────────────────────────

export interface IikoNomenclatureSize {
  productSizeId: string;
  name?: string | null;
}

export interface IikoNomenclatureProduct {
  productId: string;
  name: string;
  price: number;
  sizes: IikoNomenclatureSize[];
}

export interface IikoPaymentType {
  id: string;
  name: string;
}

export interface IikoTerminalGroup {
  id: string;
  name: string;
}

// ── Orders ────────────────────────────────────────────────────────────────────

export interface OrderItemToppingInput {
  toppingId: string;
  toppingName: string;
  price: number;
}

export interface OrderItemInput {
  menuItemId: string;
  quantity: number;
  toppings?: OrderItemToppingInput[];
  notes?: string;
  variantId?: string;
}

export interface CreateOrderRequest {
  locationId: string;
  items: OrderItemInput[];
  paymentMethod?: PaymentMethod;
  discountCode?: string;
  loyaltyPointsToSpend?: number;
}

export interface CreatePaymentRequest {
  orderId: string;
  provider: PaymentProvider;
  sandboxBehavior?: SandboxPaymentBehavior;
}

export interface PaymentInvoiceResponse {
  paymentId: string;
  invoiceId: string;
  paymentUrl: string;
  qrCode: string;
  amount: number;
  currency: Currency;
  provider: PaymentProvider;
  status: PaymentStatus;
  createdAt: string;
  expiresAt?: string;
  rawResponse?: string;
}

export interface ApplePayConfigResponse {
  enabled: boolean;
  appleMerchantId: string;
}

export interface ConfirmApplePayRequest {
  invoiceId: string;
  freedomPaymentId: string;
}

export interface PaymentStatusResponse {
  invoiceId: string;
  status: PaymentStatus;
  orderId: string;
  orderStatus: string;
  amount: number;
  currency: Currency;
  provider: PaymentProvider;
  paymentUrl: string;
  qrCode: string;
  createdAt: string;
  updatedAt?: string;
  rawResponse?: string;
}

export interface OrderItemTopping {
  toppingId: string;
  toppingName: string;
  price: number;
}

export interface OrderItem {
  id: string;
  menuItemId: string;
  menuItemName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  toppings: OrderItemTopping[];
  notes?: string;
  variantLabel?: string;
}

export interface Order {
  id: string;
  customerUserId: string;
  customerName: string;
  customerPhone?: string;
  locationId: string;
  locationName: string;
  locationTwoGisUrl?: string | null;
  status: OrderStatus;
  declineReason?: string;
  etaMinutes?: number;
  createdAt: string;
  acceptedAt?: string;
  completedAt?: string;
  paymentStatus: PaymentStatus;
  paymentMethod?: PaymentMethod;
  subtotal: number;
  discountAmount: number;
  total: number;
  discountCode?: string;
  items: OrderItem[];
  loyaltyPointsSpent?: number;
  loyaltyPointsEarned?: number;
  rating?: number;
  ratingComment?: string;
  iikoOrderSyncStatus?: IikoOrderSyncStatus;
}

export enum IikoOrderSyncStatus {
  NotPushed = 'NotPushed',
  Pushed = 'Pushed',
  Closed = 'Closed',
  Failed = 'Failed',
  SkippedUnmapped = 'SkippedUnmapped',
}

// ── Favorites ────────────────────────────────────────────────────────────────

export interface Favorite extends MenuItem {}

// ── Cart ─────────────────────────────────────────────────────────────────────

export interface CartItem {
  menuItemId: string;
  name: string;
  nameRu?: string;
  nameKk?: string;
  price: number;
  quantity: number;
  imageUrl?: string;
  selectedToppings?: OrderItemToppingInput[];
  notes?: string;
  variantId?: string;
  variantLabel?: string;
}

// ── Admin DTOs ───────────────────────────────────────────────────────────────

export interface AcceptOrderRequest {
  etaMinutes: number;
}

export interface DeclineOrderRequest {
  reason: string;
}

export interface UpdateStatusRequest {
  status: OrderStatus;
}

export interface UpdatePaymentRequest {
  paymentStatus: PaymentStatus;
}

// ── Analytics ────────────────────────────────────────────────────────────────

export interface AnalyticsResponse {
  kpis: KpiSummary;
  trends: KpiTrends;
  revenueOverTime: RevenueDataPoint[];
  topItems: TopItemDto[];
  locationPerformance: LocationPerformanceDto[];
  hourlyDistribution: HourlyDistributionDto[];
  paymentBreakdown: PaymentBreakdownDto[];
  statusBreakdown: OrderStatusBreakdownDto[];
}

export interface KpiSummary {
  totalRevenue: number;
  totalOrders: number;
  avgOrderValue: number;
  completedOrders: number;
  declinedOrders: number;
  avgPrepTimeMinutes: number;
  uniqueCustomers: number;
  newCustomers: number;
  returningCustomers: number;
  declineRatePercent: number;
  declinedRevenue: number;
}

// Percent change vs. the preceding period of equal length. Null = no comparable baseline
// (the "all" period, or the previous period had zero activity).
export interface KpiTrends {
  revenueChangePercent: number | null;
  ordersChangePercent: number | null;
  avgOrderValueChangePercent: number | null;
  avgPrepTimeChangePercent: number | null;
}

export interface RevenueDataPoint {
  label: string;
  revenue: number;
  orders: number;
}

export interface TopItemDto {
  name: string;
  quantitySold: number;
  revenue: number;
}

export interface LocationPerformanceDto {
  locationName: string;
  revenue: number;
  orders: number;
}

export interface HourlyDistributionDto {
  hour: number;
  orders: number;
}

export interface PaymentBreakdownDto {
  method: string;
  count: number;
  total: number;
}

export interface OrderStatusBreakdownDto {
  status: string;
  count: number;
}

// ── Workers ───────────────────────────────────────────────────────────────────

export interface WorkerAccount {
  id: string;
  username: string;
  role: string;
  isActive: boolean;
  createdAt: string;
}

// ── Customers (admin view) ────────────────────────────────────────────────────

export interface CustomerSummary {
  id: string;
  phone: string;
  displayName: string;
  registeredAt: string;
  orderCount: number;
  totalSpent: number;
}

export interface CustomerOrderSummary {
  id: string;
  createdAt: string;
  status: OrderStatus;
  total: number;
  locationName: string;
  itemCount: number;
  loyaltyPointsEarned?: number;
  loyaltyPointsSpent?: number;
}

export interface CustomerDetail {
  id: string;
  phone: string;
  displayName?: string;
  registeredAt: string;
  isActive: boolean;
  totalOrders: number;
  totalSpent: number;
  recentOrders: CustomerOrderSummary[];
}

// ── Group Ordering ────────────────────────────────────────────────────────────

export enum GroupCartStatus {
  Open = 'Open',
  Finalized = 'Finalized',
  Expired = 'Expired',
}

export interface GroupCartMember {
  customerId: string;
  displayName: string;
}

export interface GroupCartItem {
  id: string;
  addedByUserId: string;
  addedByName: string;
  menuItemId: string;
  menuItemName: string;
  unitPrice: number;
  quantity: number;
  toppings: OrderItemToppingInput[];
  notes?: string;
}

export interface GroupCart {
  id: string;
  code: string;
  locationName: string;
  locationId: string;
  status: GroupCartStatus;
  expiresAt: string;
  isCreator: boolean;
  members: GroupCartMember[];
  items: GroupCartItem[];
}

export interface AddGroupOrderItemRequest {
  menuItemId: string;
  menuItemName: string;
  unitPrice: number;
  quantity: number;
  toppings: OrderItemToppingInput[];
  notes?: string;
}

// ── Dashboard ─────────────────────────────────────────────────────────────────

export interface DashboardData {
  ordersToday: number;
  revenueToday: number;
  avgOrderValue: number;
  pendingCount: number;
  hourlyOrders: { hour: number; count: number }[];
}

// ── Customer stats ────────────────────────────────────────────────────────────

export interface CustomerStats {
  totalOrders: number;
  totalSpent: number;
  totalDrinks: number;
  distinctLocations: number;
  redeemedOrders: number;
  earlyOrders: number;
  lateOrders: number;
  maxDailyDrinks: number;
  maxDailyStreak: number;
  maxWeeklyStreak: number;
  maxWeekendStreak: number;
  maxRepeatItemCount: number;
}

export const EMPTY_CUSTOMER_STATS: CustomerStats = {
  totalOrders: 0,
  totalSpent: 0,
  totalDrinks: 0,
  distinctLocations: 0,
  redeemedOrders: 0,
  earlyOrders: 0,
  lateOrders: 0,
  maxDailyDrinks: 0,
  maxDailyStreak: 0,
  maxWeeklyStreak: 0,
  maxWeekendStreak: 0,
  maxRepeatItemCount: 0,
};

// ── Loyalty (iiko) ────────────────────────────────────────────────────────────

export interface LoyaltyBalance {
  enabled: boolean;
  available: boolean;
  linked: boolean;
  balance: number | null;
  earnPercent: number;
}

/** Offsite (in-shop counter) purchase — app-order earn/spend appears in order history instead. */
export interface OffsitePurchase {
  whenCreated: string;
  orderNumber: number | null;
  orderTotal: number | null;
  kztPaid: number | null;
  bonusSpent: number;
  bonusEarned: number;
  balanceAfter: number | null;
}

export interface LoyaltyHistory {
  enabled: boolean;
  available: boolean;
  linked: boolean;
  transactions: OffsitePurchase[];
}

// ── App update gate ─────────────────────────────────────────────────────────

export interface AppUpdateInfo {
  minVersionIos: string;
  minVersionAndroid: string;
  storeUrlIos: string;
  storeUrlAndroid: string;
  latestVersion?: string;
  latestNotesEn?: string;
  latestNotesRu?: string;
  latestNotesKk?: string;
}

export interface AppRelease {
  id: string;
  version: string;
  notesEn: string;
  notesRu: string;
  notesKk: string;
  isMandatory: boolean;
  createdAt: string;
}

// ── Modules (paid platform features) ───────────────────────────────────────

/** Public runtime config: keys of the modules that are switched on. */
export interface AppConfig {
  modules: string[];
  maintenance?: MaintenanceInfo;
  /** The cafe's brand; absent on servers that predate it (the apps then keep the Yurt default). */
  brand?: BrandConfig;
}

/** A cafe's brand. Colours are "#rrggbb". */
export interface BrandConfig {
  name: string;
  primaryColor: string;
  accentColor: string;
  logoUrl?: string | null;
  supportContact?: string | null;
  iconUrl?: string | null;
  splashUrl?: string | null;
  welcomeText?: string | null;
  links?: BrandLink[] | null;
  /** Corner style: 'square' | 'round'; null or 'default' keeps today's look. */
  radius?: string | null;
  /** Font: 'system' | 'serif'; null or 'default' keeps Inter. */
  font?: string | null;
}

export interface BrandLink {
  label: string;
  url: string;
}

export interface MaintenanceInfo {
  enabled: boolean;
  message: string;
}

export interface PlatformModule {
  key: string;
  enabled: boolean;
  state: ModuleState;
  validUntil: string | null;
  graceEndsAt: string | null;
  source: 'Manual' | 'Trial' | 'Paid';
  notes: string | null;
  warnings: string[];
}

export interface UpdateModuleLicenseRequest {
  enabled: boolean;
  validUntil: string | null;
  source: 'Manual' | 'Trial' | 'Paid';
  notes: string | null;
}

export interface IntegrationStatus {
  key: string;
  configured: boolean;
  enabled: boolean;
}

export type ModuleState = 'active' | 'grace' | 'expired' | 'disabled';

export interface ModuleStatusInfo {
  key: string;
  state: ModuleState;
  enabled: boolean;
  validUntil: string | null;
  graceEndsAt: string | null;
  source: string;
  notes: string | null;
}

export interface SubscriptionInfo {
  modules: ModuleStatusInfo[];
}

// ── Discount Codes ────────────────────────────────────────────────────────────

export interface DiscountCode {
  id: string;
  code: string;
  title: string;
  discountType: 'Percentage' | 'FixedAmount';
  discountValue: number;
  maxUses?: number;
  usedCount: number;
  minOrderAmount?: number;
  startsAt?: string;
  expiresAt?: string;
  isActive: boolean;
  createdAt: string;
}

export interface ValidateDiscountCodeResponse {
  isValid: boolean;
  message: string;
  discountAmount: number;
  description: string;
}

// ── Promotions ───────────────────────────────────────────────────────────────

export interface Promotion {
  id: string;
  title: string;
  description: string;
  imageUrl?: string;
  buttonLabel?: string;
  buttonUrl?: string;
  isActive: boolean;
  expiresAt?: string;
  createdAt: string;
}


export interface AuditLogEntry {
  id: string;
  action: string;
  entityType: string;
  entityId?: string;
  performedByAdminId?: string;
  performedByUsername: string;
  details?: string;
  ipAddress?: string;
  createdAt: string;
}

export interface UserReport {
  id: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  text: string;
  isResolved: boolean;
  resolvedAt?: string;
  createdAt: string;
}
