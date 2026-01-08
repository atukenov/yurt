"use client";

import { useToastNotification } from "@/components/ToastProvider";
import { useLanguage } from "@/context/LanguageContext";
import { useFavorites } from "@/hooks/useFavorites";
import { translations } from "@/lib/translations";
import type { CartItem } from "@/store/cart";
import { useCartStore } from "@/store/cart";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

export const dynamic = "force-dynamic";

export default function FavoritesPage() {
  const router = useRouter();
  const { favorites, isLoading, removeFavorite, clearFavorites } =
    useFavorites();
  const { addItem } = useCartStore();
  const { showToast } = useToastNotification();

  // Safely access language context with fallback
  let language: "en" | "ru" = "en";
  let t = translations.en.client;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.client || translations.en.client;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.client;
  }

  const [selectedSize, setSelectedSize] = useState<
    Record<string, "small" | "medium" | "large">
  >({});

  // Handle adding item to cart
  const handleAddToCart = (favoriteItem: any) => {
    const size = (selectedSize[favoriteItem.menuItemId] || "medium") as
      | "small"
      | "medium"
      | "large";

    const cartItem: CartItem = {
      id: `${favoriteItem.menuItemId}-${Date.now()}`,
      menuItemId: favoriteItem.menuItemId,
      name: favoriteItem.name,
      price: favoriteItem.price,
      quantity: 1,
      size,
      toppings: [],
    };

    addItem(cartItem);
    showToast(`${favoriteItem.name} ${t.addedToCart}`, "success");
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gray-50 p-4 pt-20">
        <div className="max-w-4xl mx-auto">
          <div className="flex items-center justify-center h-96">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-amber-600"></div>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 p-4 pt-20">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-gray-900">
              {t.myFavorites}
            </h1>
            <p className="text-gray-600 mt-1">
              {favorites.length} {favorites.length !== 1 ? t.items : t.item}
            </p>
          </div>
          <button
            onClick={() => router.push("/menu")}
            className="px-4 py-2 text-amber-600 hover:text-amber-700 font-medium"
          >
            {t.backToMenu}
          </button>
        </div>

        {favorites.length === 0 ? (
          <div className="bg-white rounded-lg shadow p-8 text-center">
            <svg
              className="w-16 h-16 mx-auto text-gray-400 mb-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"
              />
            </svg>
            <p className="text-gray-600 text-lg mb-6">{t.noFavoritesYet}</p>
            <Link
              href="/menu"
              className="inline-block px-6 py-3 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-medium"
            >
              {t.exploreMenu}
            </Link>
          </div>
        ) : (
          <>
            {/* Favorites Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
              {favorites.map((favorite) => (
                <div
                  key={favorite.menuItemId}
                  className="bg-white rounded-lg shadow hover:shadow-lg transition overflow-hidden"
                >
                  {/* Item Image Placeholder */}
                  {favorite.image && (
                    <div className="w-full h-48 bg-gray-200 overflow-hidden">
                      <img
                        src={favorite.image}
                        alt={favorite.name}
                        className="w-full h-full object-cover"
                      />
                    </div>
                  )}

                  <div className="p-4">
                    {/* Item Header */}
                    <div className="flex items-start justify-between mb-2">
                      <div>
                        <h3 className="text-lg font-bold text-gray-900">
                          {favorite.name}
                        </h3>
                        {favorite.category && (
                          <p className="text-xs text-gray-500 capitalize">
                            {favorite.category}
                          </p>
                        )}
                      </div>
                      <button
                        onClick={() => removeFavorite(favorite.menuItemId)}
                        className="text-red-500 hover:text-red-700 transition"
                        title="Remove from favorites"
                      >
                        <svg
                          className="w-6 h-6"
                          fill="currentColor"
                          viewBox="0 0 24 24"
                        >
                          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
                        </svg>
                      </button>
                    </div>

                    {/* Description */}
                    {favorite.description && (
                      <p className="text-sm text-gray-600 mb-3">
                        {favorite.description}
                      </p>
                    )}

                    {/* Price */}
                    <div className="flex items-center justify-between mb-4">
                      <span className="text-2xl font-bold text-amber-600">
                        {favorite.price.toFixed(0)} ₸
                      </span>
                      <span className="text-xs text-gray-500">
                        {t.added} {formatDate(favorite.addedAt, t)}
                      </span>
                    </div>

                    {/* Size Selection */}
                    <div className="mb-4">
                      <label className="text-xs font-medium text-gray-700 block mb-2">
                        {t.size}
                      </label>
                      <div className="flex gap-2">
                        {["small", "medium", "large"].map((size) => (
                          <button
                            key={size}
                            onClick={() =>
                              setSelectedSize((prev) => ({
                                ...prev,
                                [favorite.menuItemId]: size as
                                  | "small"
                                  | "medium"
                                  | "large",
                              }))
                            }
                            className={`flex-1 py-2 px-3 rounded text-xs font-medium capitalize transition ${
                              (selectedSize[favorite.menuItemId] ||
                                "medium") === size
                                ? "bg-[#ffd119] text-black"
                                : "bg-gray-100 text-gray-700 hover:bg-gray-200"
                            }`}
                          >
                            {size === "small" && t.small}
                            {size === "medium" && t.medium}
                            {size === "large" && t.large}
                          </button>
                        ))}
                      </div>
                    </div>

                    {/* Add to Cart Button */}
                    <button
                      onClick={() => handleAddToCart(favorite)}
                      className="w-full py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-medium text-sm"
                    >
                      {t.addToCart}
                    </button>
                  </div>
                </div>
              ))}
            </div>

            {/* Clear All Button */}
            <div className="flex justify-end">
              <button
                onClick={() => {
                  if (window.confirm(t.confirmClearFavorites)) {
                    clearFavorites();
                    showToast(t.allFavoritesCleared, "success");
                  }
                }}
                className="px-4 py-2 text-red-600 hover:text-red-700 border border-red-200 rounded-lg hover:bg-red-50 transition font-medium"
              >
                {t.clearAll}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

// Helper function to format date
function formatDate(timestamp: number, t: any): string {
  const now = Date.now();
  const diff = now - timestamp;
  const hours = Math.floor(diff / (1000 * 60 * 60));
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));

  if (hours < 1) {
    return "just now";
  } else if (hours < 24) {
    return `${hours}h ${t.ago}`;
  } else if (days < 7) {
    return `${days}d ${t.ago}`;
  } else {
    return new Date(timestamp).toLocaleDateString();
  }
}
