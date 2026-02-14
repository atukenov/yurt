// ============================================
// Entity Interfaces
// ============================================

export interface IUser {
  _id: string;
  email: string;
  name: string;
  phone?: string;
  role: "customer" | "admin";
  image?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface IMenuItem {
  _id: string;
  name: string;
  description?: string;
  category: "hot" | "cold" | "latte" | "cappuccino" | "espresso" | "specialty";
  basePrice: number;
  sizes: Array<{
    size: "small" | "medium" | "large";
    priceModifier: number;
  }>;
  image?: string;
  isAvailable: boolean;
  preparationTime: number;
  createdAt: Date;
  updatedAt: Date;
}

export interface ITopping {
  _id: string;
  name: string;
  price: number;
  category?: "syrup" | "shot" | "milk" | "topping";
  description?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface ILocation {
  _id: string;
  name: string;
  address: string;
  city: string;
  zipCode: string;
  phone?: string;
  latitude?: number;
  longitude?: number;
  workingHours: Record<string, { open: string; close: string }>;
  isActive: boolean;
  availableMenuItems: string[];
  createdAt: Date;
  updatedAt: Date;
}

export interface IOrderItem {
  menuItem: string | IMenuItem;
  quantity: number;
  size: "small" | "medium" | "large";
  toppings: string[];
  specialInstructions?: string;
  priceAtOrder: number;
}

export interface IOrder {
  _id: string;
  orderNumber: string;
  customer: string | IUser;
  location: string | ILocation;
  items: IOrderItem[];
  status: "pending" | "accepted" | "rejected" | "completed" | "cancelled";
  totalPrice: number;
  estimatedPrepTime?: number;
  rejectionReason?: string;
  rejectionComment?: string;
  paymentStatus: "pending" | "completed" | "failed";
  paymentMethod: "kaspi" | "applepay";
  notes?: string;
  createdAt: Date;
  updatedAt: Date;
}

export interface IReview {
  _id: string;
  order: string;
  customer: string | IUser;
  menuItem: string | IMenuItem;
  rating: number;
  comment?: string;
  isApproved?: boolean;
  createdAt: Date;
  updatedAt: Date;
}

export interface INotification {
  _id: string;
  order: string;
  recipient: string;
  type: "order_accepted" | "order_rejected" | "order_completed";
  message?: string;
  read: boolean;
  createdAt: Date;
  updatedAt: Date;
}

// ============================================
// Constants
// ============================================

export const MENU_CATEGORIES = [
  "hot",
  "cold",
  "latte",
  "cappuccino",
  "espresso",
  "specialty",
] as const;
export type MenuCategory = (typeof MENU_CATEGORIES)[number];

export const TOPPING_CATEGORIES = ["syrup", "shot", "milk", "topping"] as const;
export type ToppingCategory = (typeof TOPPING_CATEGORIES)[number];

export const ORDER_STATUSES = [
  "pending",
  "accepted",
  "rejected",
  "completed",
  "cancelled",
] as const;
export type OrderStatus = (typeof ORDER_STATUSES)[number];

export const PAYMENT_METHODS = ["kaspi", "applepay"] as const;
export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

export const PAYMENT_STATUSES = ["pending", "completed", "failed"] as const;
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number];

export const ITEM_SIZES = ["small", "medium", "large"] as const;
export type ItemSize = (typeof ITEM_SIZES)[number];

export const USER_ROLES = ["customer", "admin"] as const;
export type UserRole = (typeof USER_ROLES)[number];

export const TOAST_TYPES = ["success", "error", "info", "warning"] as const;
export type ToastType = (typeof TOAST_TYPES)[number];

export const NOTIFICATION_TYPES = [
  "order_accepted",
  "order_rejected",
  "order_completed",
] as const;
export type NotificationType = (typeof NOTIFICATION_TYPES)[number];

export const REJECTION_REASONS = [
  "no_milk",
  "no_coffee_beans",
  "size_unavailable",
  "equipment_issue",
  "custom",
] as const;

export const TOAST_CONFIG = {
  DEFAULT_DURATION: 4000,
  COLORS: {
    success: "bg-green-500",
    error: "bg-red-500",
    warning: "bg-yellow-500",
    info: "bg-blue-500",
  },
  ICONS: {
    success: "✓",
    error: "✕",
    warning: "⚠",
    info: "ℹ",
  },
} as const;

export const STATUS_COLORS = {
  pending: {
    bg: "bg-yellow-100",
    text: "text-yellow-800",
    label: "bg-yellow-200",
  },
  accepted: { bg: "bg-blue-100", text: "text-blue-800", label: "bg-blue-200" },
  completed: {
    bg: "bg-green-100",
    text: "text-green-800",
    label: "bg-green-200",
  },
  rejected: { bg: "bg-red-100", text: "text-red-800", label: "bg-red-200" },
  cancelled: { bg: "bg-gray-100", text: "text-gray-800", label: "bg-gray-200" },
} as const;

export const SIZE_MODIFIERS = {
  small: -0.5,
  medium: 0,
  large: 0.5,
} as const;

export const PREP_TIME_OPTIONS = [
  { value: 10, label: "10 minutes" },
  { value: 15, label: "15 minutes" },
  { value: 30, label: "30 minutes" },
] as const;

export const API_ENDPOINTS = {
  MENU_ITEMS: "/api/menu/items",
  MENU_TOPPINGS: "/api/menu/toppings",
  ADMIN_MENU: "/api/admin/menu",
  ADMIN_TOPPINGS: "/api/admin/toppings",
  ADMIN_LOCATIONS: "/api/admin/locations",
  ADMIN_ORDERS: "/api/admin/orders",
  ADMIN_ANALYTICS: "/api/admin/analytics",
  ORDERS: "/api/orders",
  LOCATIONS: "/api/locations",
  LOCATIONS_AVAILABILITY: (id: string) => `/api/locations/${id}/availability`,
  LOCATIONS_HOURS: (id: string) => `/api/locations/${id}/hours`,
  LOGIN: "/api/auth/signin",
  REGISTER: "/api/auth/register",
  PROFILE: "/api/auth/profile",
} as const;

export const PAGINATION = {
  ITEMS_PER_PAGE: 10,
  ITEMS_PER_PAGE_ADMIN: 20,
} as const;
