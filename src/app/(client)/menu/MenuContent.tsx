"use client";

import { useLanguage } from "@/context/LanguageContext";
import { translations } from "@/lib/translations";
import { useCartStore } from "@/store/cart";
import { ILocation, IMenuItem, ITopping } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { FaFilter } from "react-icons/fa";

export const dynamic = "force-dynamic";

type SortOption = "name" | "price-asc" | "price-desc" | "rating" | "prep-time";

export default function MenuContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { data: session } = useSession({
    required: true,
    onUnauthenticated() {
      router.push("/login");
    },
  });

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

  const [items, setItems] = useState<IMenuItem[]>([]);
  const [toppings, setToppings] = useState<ITopping[]>([]);
  const [locations, setLocations] = useState<ILocation[]>([]);
  const [ratings, setRatings] = useState<{ [key: string]: number[] }>({});
  const [selectedLocation, setSelectedLocation] = useState<string>("");
  const [category, setCategory] = useState<string>("");
  const [search, setSearch] = useState<string>("");
  const [searchSuggestions, setSearchSuggestions] = useState<IMenuItem[]>([]);
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [sortBy, setSortBy] = useState<SortOption>("name");
  const [favorites, setFavorites] = useState<string[]>([]);
  const [recentlyOrdered, setRecentlyOrdered] = useState<IMenuItem[]>([]);
  const [selectedItem, setSelectedItem] = useState<IMenuItem | null>(null);
  const [selectedSize, setSelectedSize] = useState<
    "small" | "medium" | "large"
  >("medium");
  const [selectedToppings, setSelectedToppings] = useState<string[]>([]);
  const [specialInstructions, setSpecialInstructions] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [loading, setLoading] = useState(true);
  const [showFilterPanel, setShowFilterPanel] = useState(false);

  const { addItem: addToCart, setLocation } = useCartStore();

  const categories = [
    "hot",
    "cold",
    "latte",
    "cappuccino",
    "espresso",
    "specialty",
  ];

  // Load favorites from localStorage
  useEffect(() => {
    const savedFavorites = localStorage.getItem("menu-favorites");
    if (savedFavorites) {
      setFavorites(JSON.parse(savedFavorites));
    }

    // Load recently ordered items
    const savedRecent = localStorage.getItem("recently-ordered");
    if (savedRecent) {
      setRecentlyOrdered(JSON.parse(savedRecent));
    }
  }, []);

  // Save favorites to localStorage
  useEffect(() => {
    localStorage.setItem("menu-favorites", JSON.stringify(favorites));
  }, [favorites]);

  const handleToggleFavorite = (itemId: string) => {
    setFavorites((prev) =>
      prev.includes(itemId)
        ? prev.filter((id) => id !== itemId)
        : [...prev, itemId]
    );
  };

  useEffect(() => {
    // Get search and location from URL params
    const urlSearch = searchParams?.get("search") || "";
    const urlLocation = searchParams?.get("location") || "";
    setSearch(urlSearch);
    if (urlLocation) {
      setSelectedLocation(urlLocation);
      setLocation(urlLocation);
    }
  }, [searchParams, setLocation]);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [itemsRes, toppingsRes, locationsRes] = await Promise.all([
          fetch(`/api/menu/items${category ? `?category=${category}` : ""}`),
          fetch("/api/menu/toppings"),
          fetch("/api/locations"),
        ]);

        if (itemsRes.ok) {
          const data = await itemsRes.json();
          setItems(data.items);

          // Fetch ratings for all items
          const newRatings: { [key: string]: number[] } = {};
          for (const item of data.items) {
            try {
              const ratingsRes = await fetch(
                `/api/reviews?menuItemId=${item._id}&approved=true`
              );
              if (ratingsRes.ok) {
                const ratingsData = await ratingsRes.json();
                newRatings[item._id] = ratingsData.reviews.map(
                  (r: any) => r.rating
                );
              }
            } catch (err) {
              console.error(`Error fetching ratings for ${item._id}:`, err);
            }
          }
          setRatings(newRatings);
        }

        if (toppingsRes.ok) {
          const data = await toppingsRes.json();
          setToppings(data.toppings);
        }

        if (locationsRes.ok) {
          const data = await locationsRes.json();
          setLocations(data.locations);
          if (data.locations.length > 0) {
            setSelectedLocation(data.locations[0]._id);
            setLocation(data.locations[0]._id);
          }
        }
      } catch (error) {
        console.error("Error fetching data:", error);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, [category, setLocation]);

  // Generate search suggestions
  useEffect(() => {
    if (search.length > 0) {
      const filtered = items.filter(
        (item) =>
          item.name.toLowerCase().includes(search.toLowerCase()) ||
          item.description?.toLowerCase().includes(search.toLowerCase())
      );
      setSearchSuggestions(filtered.slice(0, 5)); // Limit to 5 suggestions
      setShowSuggestions(true);
    } else {
      setShowSuggestions(false);
    }
  }, [search, items]);

  // Get average rating for an item
  const getAverageRating = (itemId: string) => {
    const itemRatings = ratings[itemId];
    if (!itemRatings || itemRatings.length === 0) return 0;
    return itemRatings.reduce((a, b) => a + b, 0) / itemRatings.length;
  };

  // Sort and filter items
  const filteredAndSortedItems = useMemo(() => {
    const filtered = items.filter(
      (item) =>
        item.name.toLowerCase().includes(search.toLowerCase()) ||
        item.description?.toLowerCase().includes(search.toLowerCase())
    );

    // Sort
    filtered.sort((a, b) => {
      switch (sortBy) {
        case "name":
          return a.name.localeCompare(b.name);
        case "price-asc":
          return a.basePrice - b.basePrice;
        case "price-desc":
          return b.basePrice - a.basePrice;
        case "rating":
          return getAverageRating(b._id) - getAverageRating(a._id);
        case "prep-time":
          return (a.preparationTime || 0) - (b.preparationTime || 0);
        default:
          return 0;
      }
    });

    return filtered;
  }, [items, search, sortBy, ratings]);

  const handleAddToCart = () => {
    if (!selectedItem || !selectedLocation) return;

    const cartItem = {
      id: `${selectedItem._id}-${Date.now()}`,
      menuItemId: selectedItem._id,
      name: selectedItem.name,
      price: calculatePrice(),
      quantity,
      size: selectedSize,
      toppings: selectedToppings.map((id) => {
        const topping = toppings.find((t) => t._id === id);
        return { id, name: topping?.name || "", price: topping?.price || 0 };
      }),
      specialInstructions,
    };

    addToCart(cartItem);

    // Track recently ordered
    const updatedRecent = [
      selectedItem,
      ...recentlyOrdered.filter((item) => item._id !== selectedItem._id),
    ].slice(0, 10);
    setRecentlyOrdered(updatedRecent);
    localStorage.setItem("recently-ordered", JSON.stringify(updatedRecent));

    setSelectedItem(null);
    setSelectedSize("medium");
    setSelectedToppings([]);
    setSpecialInstructions("");
    setQuantity(1);
  };

  const calculatePrice = () => {
    if (!selectedItem) return 0;
    let price = selectedItem.basePrice;

    // Add size modifier
    const size = selectedItem.sizes?.find((s) => s.size === selectedSize);
    if (size) {
      price += size.priceModifier;
    }

    // Add toppings
    selectedToppings.forEach((id) => {
      const topping = toppings.find((t) => t._id === id);
      if (topping) price += topping.price;
    });

    return price;
  };

  if (!session?.user) {
    return (
      <div className="text-center py-12">
        <p>Loading...</p>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 page-transition">
      {/* Filter Button Row with Category Selector - Mobile Optimized */}
      <div className="mb-8 flex gap-2 items-center">
        <button
          onClick={() => setShowFilterPanel(!showFilterPanel)}
          className="px-4 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition shrink-0 min-h-10 min-w-10 flex items-center justify-center"
          title="Toggle filters and sort"
        >
          <FaFilter size={18} />
        </button>

        {/* Category Selector */}
        <div className="flex gap-2 overflow-x-auto pb-2 flex-1">
          <button
            onClick={() => setCategory("")}
            className={`px-4 py-2 rounded-full whitespace-nowrap transition ${
              category === ""
                ? "bg-[#ffd119] text-black"
                : "bg-gray-200 text-gray-700 hover:bg-gray-300"
            }`}
          >
            {t.allCategories}
          </button>
          {categories.map((cat) => (
            <button
              key={cat}
              onClick={() => setCategory(cat)}
              className={`px-4 py-2 rounded-full whitespace-nowrap transition ${
                category === cat
                  ? "bg-[#ffd119] text-black"
                  : "bg-gray-200 text-gray-700 hover:bg-gray-300"
              }`}
            >
              {t.categories?.[cat as keyof typeof t.categories] || cat}
            </button>
          ))}
        </div>
      </div>

      {/* Filters and Sort Panel */}
      {showFilterPanel && (
        <div className="mb-8 bg-white rounded-lg shadow p-4 space-y-4">
          {/* Sort Options */}
          <div>
            <p className="text-sm font-medium text-gray-700 mb-2">{t.sortBy}</p>
            <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
              <button
                onClick={() => setSortBy("name")}
                className={`px-3 py-2 rounded-lg text-sm transition ${
                  sortBy === "name"
                    ? "bg-[#ffd119] text-black"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200"
                }`}
              >
                {t.nameSort}
              </button>
              <button
                onClick={() => setSortBy("price-asc")}
                className={`px-3 py-2 rounded-lg text-sm transition ${
                  sortBy === "price-asc"
                    ? "bg-[#ffd119] text-black"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200"
                }`}
              >
                {t.priceAsc}
              </button>
              <button
                onClick={() => setSortBy("price-desc")}
                className={`px-3 py-2 rounded-lg text-sm transition ${
                  sortBy === "price-desc"
                    ? "bg-[#ffd119] text-black"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200"
                }`}
              >
                {t.priceDesc}
              </button>
              <button
                onClick={() => setSortBy("rating")}
                className={`px-3 py-2 rounded-lg text-sm transition ${
                  sortBy === "rating"
                    ? "bg-[#ffd119] text-black"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200"
                }`}
              >
                {t.ratingSort} ★
              </button>
              <button
                onClick={() => setSortBy("prep-time")}
                className={`px-3 py-2 rounded-lg text-sm transition ${
                  sortBy === "prep-time"
                    ? "bg-[#ffd119] text-black"
                    : "bg-gray-100 text-gray-700 hover:bg-gray-200"
                }`}
              >
                {t.prepTimeSort} ⏱
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Recently Ordered Section */}
      {showFilterPanel && recentlyOrdered.length > 0 && (
        <div className="mb-8">
          <h2 className="text-lg font-bold text-gray-900 mb-4">
            Recently Ordered
          </h2>
          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-4">
            {recentlyOrdered.slice(0, 6).map((item) => (
              <button
                key={item._id}
                onClick={() => setSelectedItem(item)}
                className="bg-white rounded-lg shadow hover:shadow-lg transition overflow-hidden text-left"
              >
                <div className="bg-linear-to-br from-amber-100 to-orange-100 h-24 flex items-center justify-center">
                  {item.image ? (
                    <img
                      src={item.image}
                      alt={item.name}
                      className="w-full h-full object-cover"
                    />
                  ) : (
                    <span className="text-3xl">☕</span>
                  )}
                </div>
                <div className="p-2">
                  <p className="text-xs font-semibold text-gray-900 truncate">
                    {item.name}
                  </p>
                  <p className="text-xs text-amber-600 font-bold">
                    ${item.basePrice.toFixed(2)}
                  </p>
                </div>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Menu Items Grid */}
      <div className="grid grid-cols-2 md:grid-cols-2 lg:grid-cols-3 gap-6 mb-12">
        {filteredAndSortedItems.map((item) => (
          <div
            key={item._id}
            className="bg-white rounded-lg shadow hover:shadow-lg transition overflow-hidden cursor-pointer group relative"
          >
            <button
              onClick={() => handleToggleFavorite(item._id)}
              className="absolute top-2 right-2 z-10 bg-white rounded-full p-2 shadow-md hover:bg-amber-50 transition w-11.25 h-11.25"
            >
              <span
                className={
                  favorites.includes(item._id)
                    ? "text-red-500 text-lg"
                    : "text-gray-300 text-lg"
                }
              >
                ♥
              </span>
            </button>
            <div
              className="bg-linear-to-br from-amber-100 to-orange-100 h-48 flex items-center justify-center overflow-hidden"
              onClick={() => setSelectedItem(item)}
            >
              {item.image ? (
                <img
                  src={item.image}
                  alt={item.name}
                  className="w-full h-full object-cover group-hover:scale-105 transition"
                />
              ) : (
                <span className="text-6xl">☕</span>
              )}
            </div>
            <div className="p-4" onClick={() => setSelectedItem(item)}>
              <h3 className="font-bold text-lg text-gray-900">{item.name}</h3>
              <p className="text-sm text-gray-600 mb-3">{item.description}</p>

              {/* Rating Display */}
              {ratings[item._id] && ratings[item._id].length > 0 && (
                <div className="mb-3 flex items-center gap-2">
                  <div className="flex gap-0.5">
                    {[1, 2, 3, 4, 5].map((star) => (
                      <span
                        key={star}
                        className={
                          star <= Math.round(getAverageRating(item._id))
                            ? "text-yellow-400 text-sm"
                            : "text-gray-300 text-sm"
                        }
                      >
                        ★
                      </span>
                    ))}
                  </div>
                  <span className="text-xs text-gray-600">
                    {getAverageRating(item._id).toFixed(1)} (
                    {ratings[item._id].length})
                  </span>
                </div>
              )}

              <div className="flex justify-between items-center">
                <span className="text-lg sm:text-2xl font-bold text-amber-600">
                  {item.basePrice.toFixed(2)} ₸
                </span>
                <span className="text-xs bg-amber-100 text-amber-800 px-3 py-1 rounded-full">
                  {item.preparationTime} min
                </span>
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Item Detail Modal */}
      {selectedItem && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-lg max-w-md w-full max-h-screen overflow-y-auto">
            <div className="p-6">
              <div className="flex justify-between items-start mb-6">
                <h2 className="text-xl sm:text-2xl font-bold text-gray-900">
                  {selectedItem.name}
                </h2>
                <button
                  onClick={() => setSelectedItem(null)}
                  className="text-gray-500 hover:text-gray-700 text-xl sm:text-2xl"
                >
                  ×
                </button>
              </div>

              <p className="text-gray-600 mb-6">{selectedItem.description}</p>

              {/* Size Selection */}
              <div className="mb-6">
                <h3 className="font-semibold text-gray-900 mb-3">
                  {t.selectSize}
                </h3>
                <div className="space-y-2">
                  {selectedItem.sizes?.map((s) => (
                    <label key={s.size} className="flex items-center">
                      <input
                        type="radio"
                        name="size"
                        value={s.size}
                        checked={selectedSize === s.size}
                        onChange={(e) =>
                          setSelectedSize(
                            e.target.value as "small" | "medium" | "large"
                          )
                        }
                        className="w-4 h-4"
                      />
                      <span className="ml-3 capitalize text-gray-700">
                        {s.size} (+{s.priceModifier.toFixed(2)} ₸)
                      </span>
                    </label>
                  ))}
                </div>
              </div>

              {/* Toppings Selection */}
              {toppings.length > 0 && (
                <div className="mb-6">
                  <h3 className="font-semibold text-gray-900 mb-3">
                    {t.addToppings}
                  </h3>
                  <div className="space-y-4">
                    {Array.from(
                      new Set(toppings.map((t) => t.category || "Other"))
                    ).map((category) => (
                      <div key={category}>
                        <h4 className="text-sm font-medium text-gray-700 mb-2 capitalize">
                          {category}
                        </h4>
                        <div className="space-y-2 pl-2">
                          {toppings
                            .filter((t) => (t.category || "Other") === category)
                            .map((topping) => (
                              <label
                                key={topping._id}
                                className="flex items-center"
                              >
                                <input
                                  type="checkbox"
                                  checked={selectedToppings.includes(
                                    topping._id
                                  )}
                                  onChange={(e) => {
                                    if (e.target.checked) {
                                      setSelectedToppings([
                                        ...selectedToppings,
                                        topping._id,
                                      ]);
                                    } else {
                                      setSelectedToppings(
                                        selectedToppings.filter(
                                          (id) => id !== topping._id
                                        )
                                      );
                                    }
                                  }}
                                  className="w-4 h-4"
                                />
                                <span className="ml-3 text-gray-700 text-sm">
                                  {topping.name} (+
                                  {topping.price.toFixed(2)} ₸)
                                </span>
                              </label>
                            ))}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Special Instructions */}
              <div className="mb-6">
                <label className="block text-sm font-medium text-gray-700 mb-2">
                  {t.specialInstructions}
                </label>
                <textarea
                  value={specialInstructions}
                  onChange={(e) => setSpecialInstructions(e.target.value)}
                  placeholder="e.g., Extra hot, light foam..."
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:border-amber-500 focus:outline-none"
                  rows={3}
                />
              </div>

              {/* Quantity */}
              <div className="mb-6">
                <h3 className="font-semibold text-gray-900 mb-3">
                  {t.quantity}
                </h3>
                <div className="flex items-center gap-4">
                  <button
                    onClick={() => setQuantity(Math.max(1, quantity - 1))}
                    className="px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-100"
                  >
                    −
                  </button>
                  <span className="text-xl font-semibold">{quantity}</span>
                  <button
                    onClick={() => setQuantity(quantity + 1)}
                    className="px-4 py-2 border border-gray-300 rounded-lg hover:bg-gray-100"
                  >
                    +
                  </button>
                </div>
              </div>

              {/* Price and Add to Cart */}
              <div className="border-t pt-6">
                <div className="mb-4 text-center">
                  <p className="text-gray-600">Total</p>
                  <p className="text-3xl font-bold text-amber-600">
                    {(calculatePrice() * quantity).toFixed(2)} ₸
                  </p>
                </div>
                <button
                  onClick={handleAddToCart}
                  className="w-full px-4 py-3 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-semibold"
                >
                  {t.addToCart}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
