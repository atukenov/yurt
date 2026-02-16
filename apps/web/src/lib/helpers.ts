/**
 * Utility Functions for Data Manipulation & Type Checking
 * Common helpers used across components
 */

import { IOrder, IOrderItem, IUser } from "@/types";

/**
 * Safely extract name from object or return empty string
 */
export const getName = (obj: unknown): string => {
  return obj && typeof obj === "object" && "name" in obj
    ? String(obj.name)
    : "";
};

/**
 * Safely extract email from user object
 */
export const getEmail = (user: IUser | string | null | undefined): string => {
  if (!user) return "";
  return typeof user === "object" && "email" in user ? user.email : "";
};

/**
 * Safely extract phone from user object
 */
export const getPhone = (user: IUser | string | null | undefined): string => {
  if (!user) return "";
  return typeof user === "object" && "phone" in user ? user.phone || "" : "";
};

/**
 * Get customer display name
 */
export const getCustomerName = (customer: IOrder["customer"]): string => {
  return customer && typeof customer === "object" ? customer.name : "";
};

/**
 * Get customer email
 */
export const getCustomerEmail = (customer: IOrder["customer"]): string => {
  return customer && typeof customer === "object" ? customer.email : "";
};

/**
 * Get customer phone
 */
export const getCustomerPhone = (customer: IOrder["customer"]): string => {
  return customer && typeof customer === "object" ? customer.phone || "" : "";
};

/**
 * Get location name
 */
export const getLocationName = (location: IOrder["location"]): string => {
  return location && typeof location === "object" ? location.name : "";
};

/**
 * Get menu item name
 */
export const getMenuItemName = (menuItem: IOrderItem["menuItem"]): string => {
  return menuItem && typeof menuItem === "object" ? menuItem.name || "" : "";
};

/**
 * Get topping name (handles both object and string)
 */
export const getToppingName = (topping: unknown): string => {
  if (!topping) return "";
  if (typeof topping === "string") return topping;
  if (typeof topping === "object" && "name" in topping) {
    return String(topping.name);
  }
  return "";
};

/**
 * Format date to locale string
 */
export const formatDate = (
  date: string | Date | undefined,
  options: "date" | "time" | "full" = "date"
): string => {
  if (!date) return "";

  const dateObj = typeof date === "string" ? new Date(date) : date;

  if (isNaN(dateObj.getTime())) return "";

  switch (options) {
    case "date":
      return dateObj.toLocaleDateString();
    case "time":
      return dateObj.toLocaleTimeString();
    case "full":
      return dateObj.toLocaleString();
    default:
      return dateObj.toLocaleDateString();
  }
};

/**
 * Capitalize first letter of string
 */
export const capitalize = (str: string): string => {
  if (!str) return "";
  return str.charAt(0).toUpperCase() + str.slice(1);
};

/**
 * Format price to currency string
 */
export const formatPrice = (price: number | undefined): string => {
  if (price === undefined || price === null) return "$0.00";
  return `$${price.toFixed(2)}`;
};

/**
 * Get status badge color classes
 */
export const getStatusColor = (
  status: string
): { bg: string; text: string; label: string } => {
  const colors: Record<string, { bg: string; text: string; label: string }> = {
    pending: {
      bg: "bg-yellow-50",
      text: "text-yellow-800",
      label: "bg-yellow-200",
    },
    accepted: { bg: "bg-blue-50", text: "text-blue-800", label: "bg-blue-200" },
    completed: {
      bg: "bg-green-50",
      text: "text-green-800",
      label: "bg-green-200",
    },
    rejected: { bg: "bg-red-50", text: "text-red-800", label: "bg-red-200" },
    cancelled: {
      bg: "bg-gray-50",
      text: "text-gray-800",
      label: "bg-gray-200",
    },
  };

  return colors[status] || colors.pending;
};

/**
 * Truncate text to specific length
 */
export const truncate = (text: string | undefined, length: number): string => {
  if (!text) return "";
  return text.length > length ? `${text.substring(0, length)}...` : text;
};

/**
 * Check if value is empty (null, undefined, or empty string)
 */
export const isEmpty = (value: unknown): boolean => {
  return (
    value === null ||
    value === undefined ||
    value === "" ||
    (Array.isArray(value) && value.length === 0) ||
    (typeof value === "object" && Object.keys(value).length === 0)
  );
};

/**
 * Safe object property access
 */
export const safeGet = (
  obj: unknown,
  path: string,
  defaultValue: unknown = null
): unknown => {
  try {
    return (
      (path.split(".").reduce((current: unknown, prop) => {
        if (current && typeof current === "object" && prop in current) {
          return (current as Record<string, unknown>)[prop];
        }
        return undefined;
      }, obj) as unknown) ?? defaultValue
    );
  } catch {
    return defaultValue;
  }
};
