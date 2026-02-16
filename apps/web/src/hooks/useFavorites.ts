import { useCallback, useEffect, useState } from "react";

export interface FavoriteItem {
  menuItemId: string;
  name: string;
  description?: string;
  price: number;
  image?: string;
  category?: string;
  addedAt: number;
}

const FAVORITES_STORAGE_KEY = "yurt_favorites";

export function useFavorites() {
  const [favorites, setFavorites] = useState<FavoriteItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Initialize from localStorage
  useEffect(() => {
    try {
      const stored = localStorage.getItem(FAVORITES_STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        setFavorites(Array.isArray(parsed) ? parsed : []);
      }
    } catch (error) {
      console.error("[Favorites] Failed to load from localStorage:", error);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Save to localStorage whenever favorites change
  useEffect(() => {
    if (!isLoading) {
      try {
        localStorage.setItem(FAVORITES_STORAGE_KEY, JSON.stringify(favorites));
      } catch (error) {
        console.error("[Favorites] Failed to save to localStorage:", error);
      }
    }
  }, [favorites, isLoading]);

  // Add item to favorites
  const addFavorite = useCallback((item: Omit<FavoriteItem, "addedAt">) => {
    setFavorites((prev) => {
      const exists = prev.find((fav) => fav.menuItemId === item.menuItemId);
      if (exists) return prev;

      return [
        ...prev,
        {
          ...item,
          addedAt: Date.now(),
        },
      ];
    });
  }, []);

  // Remove item from favorites
  const removeFavorite = useCallback((menuItemId: string) => {
    setFavorites((prev) => prev.filter((fav) => fav.menuItemId !== menuItemId));
  }, []);

  // Check if item is favorite
  const isFavorite = useCallback(
    (menuItemId: string) => {
      return favorites.some((fav) => fav.menuItemId === menuItemId);
    },
    [favorites]
  );

  // Toggle favorite status
  const toggleFavorite = useCallback(
    (item: Omit<FavoriteItem, "addedAt">) => {
      if (isFavorite(item.menuItemId)) {
        removeFavorite(item.menuItemId);
      } else {
        addFavorite(item);
      }
    },
    [isFavorite, removeFavorite, addFavorite]
  );

  // Clear all favorites
  const clearFavorites = useCallback(() => {
    setFavorites([]);
  }, []);

  return {
    favorites,
    isLoading,
    addFavorite,
    removeFavorite,
    isFavorite,
    toggleFavorite,
    clearFavorites,
    count: favorites.length,
  };
}
