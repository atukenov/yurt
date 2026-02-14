// ============================================
// Socket.IO Configuration
// ============================================

export const SOCKET_NAMESPACE = "/orders";

export const SOCKET_EVENTS = {
  // Client → Server
  USER_JOIN: "user-join",

  // Server → Client
  ORDER_CREATED: "order-created",
  ORDER_UPDATED: "order-updated",
  ORDER_DELETED: "order-deleted",
  ORDER_STATUS_CHANGED: "order-status-changed",
} as const;

export const SOCKET_ROOMS = {
  ADMIN: "admin",
  customer: (userId: string) => `customer-${userId}`,
} as const;

// ============================================
// Environment Variable Keys
// ============================================

export const ENV_KEYS = {
  MONGODB_URI: "MONGODB_URI",
  NEXTAUTH_SECRET: "NEXTAUTH_SECRET",
  NEXTAUTH_URL: "NEXTAUTH_URL",
  API_URL: "NEXT_PUBLIC_API_URL",
  PORT: "PORT",
  NODE_ENV: "NODE_ENV",
} as const;

// ============================================
// CORS Configuration
// ============================================

export const DEFAULT_CORS_OPTIONS = {
  methods: ["GET", "POST", "PUT", "DELETE", "PATCH"],
  credentials: true,
} as const;

// ============================================
// Cache TTLs (in milliseconds)
// ============================================

export const CACHE_TTLS = {
  MENU_ITEMS: 5 * 60 * 1000, // 5 minutes
  TOPPINGS: 5 * 60 * 1000, // 5 minutes
  LOCATIONS: 10 * 60 * 1000, // 10 minutes
  ANALYTICS: 2 * 60 * 1000, // 2 minutes
} as const;
