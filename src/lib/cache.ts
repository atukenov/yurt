/**
 * In-memory cache utility with TTL (Time To Live) support
 * Provides a simple, efficient caching layer for API responses
 */

interface CacheEntry<T> {
  data: T;
  expiresAt: number;
}

class Cache {
  private store = new Map<string, CacheEntry<unknown>>();

  /**
   * Get a cached value
   * @param key - Cache key
   * @returns Cached data or null if not found or expired
   */
  get<T>(key: string): T | null {
    const entry = this.store.get(key);

    if (!entry) {
      return null;
    }

    // Check if entry has expired
    if (Date.now() > entry.expiresAt) {
      this.store.delete(key);
      return null;
    }

    return entry.data as T;
  }

  /**
   * Set a cached value with TTL
   * @param key - Cache key
   * @param data - Data to cache
   * @param ttlSeconds - Time to live in seconds (default: 300 = 5 minutes)
   */
  set<T>(key: string, data: T, ttlSeconds: number = 300): void {
    const expiresAt = Date.now() + ttlSeconds * 1000;
    this.store.set(key, { data, expiresAt });
  }

  /**
   * Delete a cached value
   * @param key - Cache key
   */
  delete(key: string): void {
    this.store.delete(key);
  }

  /**
   * Clear all cached values
   */
  clear(): void {
    this.store.clear();
  }

  /**
   * Get cache statistics
   */
  getStats() {
    let validEntries = 0;
    let expiredEntries = 0;

    for (const entry of this.store.values()) {
      if (Date.now() > entry.expiresAt) {
        expiredEntries++;
      } else {
        validEntries++;
      }
    }

    return {
      totalEntries: this.store.size,
      validEntries,
      expiredEntries,
    };
  }

  /**
   * Clean up expired entries (useful for preventing memory leaks)
   */
  cleanup(): void {
    const now = Date.now();
    for (const [key, entry] of this.store.entries()) {
      if (now > entry.expiresAt) {
        this.store.delete(key);
      }
    }
  }
}

// Singleton instance
const cache = new Cache();

// Run cleanup every 5 minutes
if (typeof window === "undefined") {
  // Only run on server side
  setInterval(() => {
    cache.cleanup();
  }, 5 * 60 * 1000);
}

export default cache;

/**
 * Cache key generators for consistency
 */
export const cacheKeys = {
  // Menu items - includes query parameters in key
  menuItems: (category?: string, search?: string): string => {
    const params = [category, search].filter(Boolean).join(":");
    return `menu:items${params ? `:${params}` : ""}`;
  },

  // Toppings
  toppings: (): string => "menu:toppings",

  // Locations
  locations: (): string => "locations:active",

  // Admin locations
  adminLocations: (): string => "locations:admin",
};
