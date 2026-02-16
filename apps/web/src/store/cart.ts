import { create } from "zustand";
import { persist } from "zustand/middleware";

export interface CartItem {
  id: string;
  menuItemId: string;
  name: string;
  price: number;
  quantity: number;
  size: "small" | "medium" | "large";
  toppings: Array<{ id: string; name: string; price: number }>;
  specialInstructions?: string;
}

interface AbandonedCart {
  items: CartItem[];
  locationId: string | null;
  createdAt: number;
}

interface CartStore {
  items: CartItem[];
  locationId: string | null;
  lastSavedAt: number;
  abandonedCarts: AbandonedCart[];
  addItem: (item: CartItem) => void;
  removeItem: (id: string) => void;
  updateItem: (id: string, updates: Partial<CartItem>) => void;
  clearCart: () => void;
  setLocation: (locationId: string) => void;
  getTotalPrice: () => number;
  getEstimatedTime: () => number;
  isLocationSelected: () => boolean;
  saveAbandonedCart: () => void;
  getAbandonedCarts: () => AbandonedCart[];
  restoreAbandonedCart: (index: number) => void;
  clearAbandonedCart: (index: number) => void;
}

export const useCartStore = create<CartStore>()(
  persist(
    (set, get) => ({
      items: [],
      locationId: null,
      lastSavedAt: Date.now(),
      abandonedCarts: [],

      addItem: (item) =>
        set((state) => {
          const existingItem = state.items.find((i) => i.id === item.id);
          return {
            items:
              existingItem && existingItem.size === item.size
                ? state.items.map((i) =>
                    i.id === item.id
                      ? { ...i, quantity: i.quantity + item.quantity }
                      : i
                  )
                : [...state.items, item],
            lastSavedAt: Date.now(),
          };
        }),

      removeItem: (id) =>
        set((state) => ({
          items: state.items.filter((item) => item.id !== id),
          lastSavedAt: Date.now(),
        })),

      updateItem: (id, updates) =>
        set((state) => ({
          items: state.items.map((item) =>
            item.id === id ? { ...item, ...updates } : item
          ),
          lastSavedAt: Date.now(),
        })),

      clearCart: () =>
        set({
          items: [],
          locationId: null,
          lastSavedAt: Date.now(),
        }),

      setLocation: (locationId) =>
        set((state) => ({
          locationId,
          lastSavedAt: Date.now(),
        })),

      getTotalPrice: () => {
        const items = get().items;
        return items.reduce(
          (total, item) => total + item.price * item.quantity,
          0
        );
      },

      getEstimatedTime: () => {
        // Simple estimation: 5 min base + 2 min per item
        const itemCount = get().items.reduce(
          (sum, item) => sum + item.quantity,
          0
        );
        return 5 + itemCount * 2;
      },

      isLocationSelected: () => {
        return get().locationId !== null && get().locationId !== "";
      },

      saveAbandonedCart: () => {
        set((state) => {
          // Only save if cart has items
          if (state.items.length === 0) return state;

          const newAbandonedCart: AbandonedCart = {
            items: state.items,
            locationId: state.locationId,
            createdAt: Date.now(),
          };

          // Keep only last 5 abandoned carts
          const updatedAbandonedCarts = [
            newAbandonedCart,
            ...state.abandonedCarts.slice(0, 4),
          ];

          return {
            abandonedCarts: updatedAbandonedCarts,
          };
        });
      },

      getAbandonedCarts: () => {
        return get().abandonedCarts;
      },

      restoreAbandonedCart: (index: number) => {
        set((state) => {
          const abandoned = state.abandonedCarts[index];
          if (!abandoned) return state;

          return {
            items: abandoned.items,
            locationId: abandoned.locationId,
            lastSavedAt: Date.now(),
            abandonedCarts: state.abandonedCarts.filter((_, i) => i !== index),
          };
        });
      },

      clearAbandonedCart: (index: number) => {
        set((state) => ({
          abandonedCarts: state.abandonedCarts.filter((_, i) => i !== index),
        }));
      },
    }),
    {
      name: "cart-storage",
    }
  )
);
