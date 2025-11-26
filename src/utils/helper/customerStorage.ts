/**
 * Customer Storage Helper
 * Manages customer credentials in localStorage for auto-login functionality
 */

const STORAGE_KEY = "yurt_customer_auth";

export type StoredCustomer = {
  phone: string;
  fname: string;
  lname: string;
  restaurant: string;
  lastLogin: string;
};

/**
 * Save customer credentials to localStorage
 */
export const saveCustomerToStorage = (
  customer: Omit<StoredCustomer, "lastLogin">
) => {
  try {
    if (typeof window === "undefined") return;

    const data: StoredCustomer = {
      ...customer,
      lastLogin: new Date().toISOString(),
    };

    localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
  } catch (error) {
    console.error("Failed to save customer to storage:", error);
  }
};

/**
 * Get customer credentials from localStorage
 */
export const getCustomerFromStorage = (): StoredCustomer | null => {
  try {
    if (typeof window === "undefined") return null;

    const data = localStorage.getItem(STORAGE_KEY);
    if (!data) return null;

    const customer: StoredCustomer = JSON.parse(data);

    // Check if data is older than 30 days
    const lastLogin = new Date(customer.lastLogin);
    const daysSinceLogin =
      (Date.now() - lastLogin.getTime()) / (1000 * 60 * 60 * 24);

    if (daysSinceLogin > 30) {
      clearCustomerStorage();
      return null;
    }

    return customer;
  } catch (error) {
    console.error("Failed to get customer from storage:", error);
    return null;
  }
};

/**
 * Clear customer credentials from localStorage
 */
export const clearCustomerStorage = () => {
  try {
    if (typeof window === "undefined") return;
    localStorage.removeItem(STORAGE_KEY);
  } catch (error) {
    console.error("Failed to clear customer storage:", error);
  }
};

/**
 * Update last login timestamp
 */
export const updateLastLogin = () => {
  try {
    if (typeof window === "undefined") return;

    const customer = getCustomerFromStorage();
    if (!customer) return;

    customer.lastLogin = new Date().toISOString();
    localStorage.setItem(STORAGE_KEY, JSON.stringify(customer));
  } catch (error) {
    console.error("Failed to update last login:", error);
  }
};

/**
 * Check if stored customer matches current restaurant
 */
export const isStoredCustomerForRestaurant = (
  restaurantUsername: string
): boolean => {
  const customer = getCustomerFromStorage();
  return customer?.restaurant === restaurantUsername;
};
