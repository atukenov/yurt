/**
 * Global Constants for the Application
 * Centralized source of truth for all enums, categories, and fixed values
 */

// Menu item categories
export const MENU_CATEGORIES = [
  "hot",
  "cold",
  "latte",
  "cappuccino",
  "espresso",
  "specialty",
] as const;

export type MenuCategory = (typeof MENU_CATEGORIES)[number];

// Topping categories
export const TOPPING_CATEGORIES = ["syrup", "shot", "milk", "topping"] as const;
export type ToppingCategory = (typeof TOPPING_CATEGORIES)[number];

// Order statuses
export const ORDER_STATUSES = [
  "pending",
  "accepted",
  "rejected",
  "completed",
  "cancelled",
] as const;

export type OrderStatus = (typeof ORDER_STATUSES)[number];

// Payment methods
export const PAYMENT_METHODS = ["cash", "card", "stripe"] as const;
export type PaymentMethod = (typeof PAYMENT_METHODS)[number];

// Payment statuses
export const PAYMENT_STATUSES = ["pending", "completed", "failed"] as const;
export type PaymentStatus = (typeof PAYMENT_STATUSES)[number];

// Item sizes
export const ITEM_SIZES = ["small", "medium", "large"] as const;
export type ItemSize = (typeof ITEM_SIZES)[number];

// User roles
export const USER_ROLES = ["customer", "admin"] as const;
export type UserRole = (typeof USER_ROLES)[number];

// Toast types
export const TOAST_TYPES = ["success", "error", "info", "warning"] as const;
export type ToastType = (typeof TOAST_TYPES)[number];

// Notification types
export const NOTIFICATION_TYPES = [
  "order_accepted",
  "order_rejected",
  "order_completed",
] as const;
export type NotificationType = (typeof NOTIFICATION_TYPES)[number];

// Rejection reasons
export const REJECTION_REASONS = [
  "no_milk",
  "no_coffee_beans",
  "size_unavailable",
  "equipment_issue",
  "custom",
] as const;

// Toast configuration
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

// Status colors for UI
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

// Size price modifiers
export const SIZE_MODIFIERS = {
  small: -0.5,
  medium: 0,
  large: 0.5,
} as const;

// Prep time options (in minutes)
export const PREP_TIME_OPTIONS = [
  { value: 10, label: "10 minutes" },
  { value: 15, label: "15 minutes" },
  { value: 30, label: "30 minutes" },
] as const;

// API endpoints
export const API_ENDPOINTS = {
  // Menu
  MENU_ITEMS: "/api/menu/items",
  MENU_TOPPINGS: "/api/menu/toppings",

  // Admin
  ADMIN_MENU: "/api/admin/menu",
  ADMIN_TOPPINGS: "/api/admin/toppings",
  ADMIN_LOCATIONS: "/api/admin/locations",
  ADMIN_ORDERS: "/api/admin/orders",
  ADMIN_ANALYTICS: "/api/admin/analytics",

  // Orders
  ORDERS: "/api/orders",

  // Locations
  LOCATIONS: "/api/locations",
  LOCATIONS_AVAILABILITY: (id: string) => `/api/locations/${id}/availability`,
  LOCATIONS_HOURS: (id: string) => `/api/locations/${id}/hours`,

  // Auth
  LOGIN: "/api/auth/signin",
  REGISTER: "/api/auth/register",
  PROFILE: "/api/auth/profile",
} as const;

// Default pagination
export const PAGINATION = {
  ITEMS_PER_PAGE: 10,
  ITEMS_PER_PAGE_ADMIN: 20,
} as const;
