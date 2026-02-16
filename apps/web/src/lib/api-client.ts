/**
 * API Client for communicating with the NestJS backend.
 *
 * - Automatically attaches the NextAuth session token as a Bearer token
 * - Handles JSON serialization/deserialization
 * - Provides typed helper methods for common HTTP verbs
 *
 * Usage:
 *   import { apiClient } from '@/lib/api-client';
 *   const { items } = await apiClient.get('/menu/items');
 *   const { order } = await apiClient.post('/orders', orderData);
 */

// Backend base URL — configurable via env var, defaults to localhost:4000
const BACKEND_URL =
  process.env.NEXT_PUBLIC_BACKEND_URL || "http://localhost:4000";

/**
 * Get the Bearer token from the NextAuth session.
 * NextAuth stores the encrypted JWT in the session-token cookie.
 * We read it directly from cookies since we need the raw JWE token
 * (not the decoded session object) for the NestJS backend to decrypt.
 */
function getSessionToken(): string | null {
  if (typeof document === "undefined") return null;

  const cookies = document.cookie.split(";").reduce(
    (acc, cookie) => {
      const trimmed = cookie.trim();
      const eqIndex = trimmed.indexOf("=");
      if (eqIndex === -1) return acc;
      const key = trimmed.substring(0, eqIndex);
      const value = trimmed.substring(eqIndex + 1);
      acc[key] = value;
      return acc;
    },
    {} as Record<string, string>
  );

  // NextAuth uses different cookie names in dev vs production
  return (
    cookies["next-auth.session-token"] ||
    cookies["__Secure-next-auth.session-token"] ||
    null
  );
}

interface ApiClientOptions {
  headers?: Record<string, string>;
  skipAuth?: boolean;
}

async function request<T = any>(
  method: string,
  path: string,
  body?: any,
  options: ApiClientOptions = {}
): Promise<T> {
  const url = `${BACKEND_URL}/api${path}`;

  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...options.headers,
  };

  // Attach Bearer token unless explicitly skipped
  if (!options.skipAuth) {
    const token = getSessionToken();
    if (token) {
      headers["Authorization"] = `Bearer ${token}`;
    }
  }

  const fetchOptions: RequestInit = {
    method,
    headers,
    credentials: "include", // Include cookies for cross-origin requests
  };

  if (body && method !== "GET") {
    fetchOptions.body = JSON.stringify(body);
  }

  const response = await fetch(url, fetchOptions);

  // Handle non-JSON responses
  const contentType = response.headers.get("content-type");
  if (!contentType?.includes("application/json")) {
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    return {} as T;
  }

  const data = await response.json();

  if (!response.ok) {
    // Preserve the error format from the backend: { error: string }
    const error = new Error(data.error || `HTTP ${response.status}`);
    (error as any).status = response.status;
    (error as any).data = data;
    throw error;
  }

  return data as T;
}

export const apiClient = {
  get: <T = any>(path: string, options?: ApiClientOptions) =>
    request<T>("GET", path, undefined, options),

  post: <T = any>(path: string, body?: any, options?: ApiClientOptions) =>
    request<T>("POST", path, body, options),

  put: <T = any>(path: string, body?: any, options?: ApiClientOptions) =>
    request<T>("PUT", path, body, options),

  patch: <T = any>(path: string, body?: any, options?: ApiClientOptions) =>
    request<T>("PATCH", path, body, options),

  delete: <T = any>(path: string, options?: ApiClientOptions) =>
    request<T>("DELETE", path, undefined, options),
};

/**
 * Helper to build query strings from an object.
 * Filters out undefined/null values.
 */
export function buildQueryString(params: Record<string, any>): string {
  const filtered = Object.entries(params).filter(
    ([, v]) => v !== undefined && v !== null && v !== ""
  );
  if (filtered.length === 0) return "";
  return (
    "?" +
    new URLSearchParams(filtered.map(([k, v]) => [k, String(v)])).toString()
  );
}
