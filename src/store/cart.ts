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

interface CartStore {
  items: CartItem[];
  locationId: string | null;
  addItem: (item: CartItem) => void;
  removeItem: (id: string) => void;
  updateItem: (id: string, updates: Partial<CartItem>) => void;
  clearCart: () => void;
  setLocation: (locationId: string) => void;
  getTotalPrice: () => number;
  getEstimatedTime: () => number;
}

export const useCartStore = create<CartStore>()(
  persist(
    (set, get) => ({
      items: [],
      locationId: null,

      addItem: (item) =>
        set((state) => {
          const existingItem = state.items.find((i) => i.id === item.id);
          if (existingItem) {
            return {
              items: state.items.map((i) =>
                i.id === item.id
                  ? { ...i, quantity: i.quantity + item.quantity }
                  : i
              ),
            };
          }
          return { items: [...state.items, item] };
        }),

      removeItem: (id) =>
        set((state) => ({
          items: state.items.filter((item) => item.id !== id),
        })),

      updateItem: (id, updates) =>
        set((state) => ({
          items: state.items.map((item) =>
            item.id === id ? { ...item, ...updates } : item
          ),
        })),

      clearCart: () => set({ items: [] }),

      setLocation: (locationId) => set({ locationId }),

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
    }),
    {
      name: "cart-storage",
    }
  )
);
