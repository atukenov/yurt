"use client";

import { ItemDetailModal } from "@/components/ItemDetailModal";
import { useToastNotification } from "@/components/ToastProvider";
import { useLanguage } from "@/context/LanguageContext";
import { translations, type Language } from "@/lib/translations";
import { useCartStore } from "@/store/cart";
import type { IMenuItem, ITopping } from "@/types";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { MdFavorite } from "react-icons/md";

export const dynamic = "force-dynamic";

export default function FavoritesPage() {
  const router = useRouter();
  const { addItem } = useCartStore();
  const { showToast } = useToastNotification();
  const [favorites, setFavorites] = useState<IMenuItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedItem, setSelectedItem] = useState<IMenuItem | null>(null);
  const [toppings, setToppings] = useState<ITopping[]>([]);
  const [selectedSize, setSelectedSize] = useState<
    "small" | "medium" | "large"
  >("medium");
  const [selectedToppings, setSelectedToppings] = useState<string[]>([]);
  const [specialInstructions, setSpecialInstructions] = useState("");
  const [quantity, setQuantity] = useState(1);

  // Safely access language context with fallback
  let language: Language = "en";
  let t = translations.en.client;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.client || translations.en.client;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.client;
  }

  // Load favorites from localStorage
  useEffect(() => {
    const loadFavorites = async () => {
      try {
        const savedFavorites = localStorage.getItem("menu-favorites");
        if (savedFavorites && savedFavorites !== "[]") {
          const favoriteIds = JSON.parse(savedFavorites);

          if (favoriteIds.length > 0) {
            // Fetch all menu items
            const itemsRes = await fetch("/api/menu/items");
            if (itemsRes.ok) {
              const itemsData = await itemsRes.json();

              // Filter to only show favorites
              const favoriteItems = itemsData.items.filter((item: IMenuItem) =>
                favoriteIds.includes(item._id)
              );

              setFavorites(favoriteItems);
            }

            // Fetch toppings
            const toppingsRes = await fetch("/api/menu/toppings");
            if (toppingsRes.ok) {
              const toppingsData = await toppingsRes.json();
              setToppings(toppingsData.toppings || []);
            }
          }
        }
      } catch (error) {
        console.error("Error loading favorites:", error);
      } finally {
        setIsLoading(false);
      }
    };

    loadFavorites();
  }, []);

  // Handle removing favorite
  const handleRemoveFavorite = (itemId: string) => {
    const updatedFavorites = favorites.filter((fav) => fav._id !== itemId);
    setFavorites(updatedFavorites);

    // Update localStorage
    const savedFavorites = localStorage.getItem("menu-favorites");
    if (savedFavorites) {
      const favoriteIds = JSON.parse(savedFavorites);
      const newFavoriteIds = favoriteIds.filter((id: string) => id !== itemId);
      localStorage.setItem("menu-favorites", JSON.stringify(newFavoriteIds));
    }

    showToast(t.removeFromFavorites, "success");
  };

  // Handle opening modal
  const handleOpenModal = (item: IMenuItem) => {
    setSelectedItem(item);
    setSelectedSize("medium");
    setSelectedToppings([]);
    setSpecialInstructions("");
    setQuantity(1);
  };

  // Handle closing modal
  const handleCloseModal = () => {
    setSelectedItem(null);
    setSelectedSize("medium");
    setSelectedToppings([]);
    setSpecialInstructions("");
    setQuantity(1);
  };

  const calculatePrice = (): number => {
    if (!selectedItem) return 0;
    let price = selectedItem.basePrice;

    // Add size modifier
    const size = selectedItem.sizes?.find((s) => s.size === selectedSize);
    if (size) {
      price += size.priceModifier;
    }

    // Add toppings
    const selectedToppingObjects = toppings.filter((t) =>
      selectedToppings.includes(t._id)
    );
    price += selectedToppingObjects.reduce((sum, t) => sum + t.price, 0);

    return price;
  };

  const handleAddToCart = () => {
    if (selectedItem) {
      addItem({
        id: `${selectedItem._id}-${Date.now()}`,
        menuItemId: selectedItem._id,
        name: selectedItem.name,
        price: calculatePrice(),
        size: selectedSize,
        toppings: toppings
          .filter((t) => selectedToppings.includes(t._id))
          .map((t) => ({ id: t._id, name: t.name, price: t.price })),
        quantity,
        specialInstructions,
      });
      showToast(t.addedToCart, "success");
      handleCloseModal();
    }
  };

  // Handle clearing all favorites
  const handleClearFavorites = () => {
    if (window.confirm(t.confirmClearFavorites)) {
      setFavorites([]);
      localStorage.setItem("menu-favorites", JSON.stringify([]));
      showToast(t.allFavoritesCleared, "success");
    }
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
            <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mb-8">
              {favorites.map((favorite) => (
                <div
                  key={favorite._id}
                  className="bg-white rounded-lg shadow hover:shadow-lg transition overflow-hidden"
                >
                  {/* Item Image */}
                  <div className="w-full h-48 bg-gray-200 overflow-hidden relative group">
                    {favorite.image ? (
                      <img
                        src={favorite.image}
                        alt={favorite.name}
                        className="w-full h-full object-cover"
                      />
                    ) : (
                      <div className="w-full h-full bg-gradient-to-br from-amber-100 to-orange-100 flex items-center justify-center">
                        <span className="text-4xl">☕</span>
                      </div>
                    )}
                    <button
                      onClick={() => handleRemoveFavorite(favorite._id)}
                      className="absolute top-2 right-2 bg-white rounded-full p-2 shadow-md hover:bg-red-50 transition"
                    >
                      <MdFavorite className="text-red-500 text-xl" />
                    </button>
                  </div>

                  <div className="p-4">
                    {/* Item Header */}
                    <div className="mb-2">
                      <h3 className="text-lg font-bold text-gray-900">
                        {favorite.name}
                      </h3>
                      {favorite.category && (
                        <p className="text-xs text-gray-500 capitalize">
                          {favorite.category}
                        </p>
                      )}
                    </div>

                    {/* Description */}
                    {favorite.description && (
                      <p className="text-sm text-gray-600 mb-3">
                        {favorite.description}
                      </p>
                    )}

                    {/* Price */}
                    <div className="mb-4">
                      <span className="text-2xl font-bold text-amber-600">
                        {favorite.basePrice.toFixed(0)} ₸
                      </span>
                    </div>

                    {/* Add Button */}
                    <button
                      onClick={() => handleOpenModal(favorite)}
                      className="w-full py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-medium text-sm"
                    >
                      {t.addToCart}
                    </button>
                  </div>
                </div>
              ))}
            </div>
          </>
        )}
      </div>

      {/* Item Detail Modal */}
      {selectedItem && (
        <ItemDetailModal
          selectedItem={selectedItem}
          onClose={handleCloseModal}
          selectedSize={selectedSize}
          setSelectedSize={setSelectedSize}
          selectedToppings={selectedToppings}
          setSelectedToppings={setSelectedToppings}
          specialInstructions={specialInstructions}
          setSpecialInstructions={setSpecialInstructions}
          quantity={quantity}
          setQuantity={setQuantity}
          toppings={toppings}
          calculatePrice={calculatePrice}
          handleAddToCart={handleAddToCart}
          t={t}
        />
      )}
    </div>
  );
}
