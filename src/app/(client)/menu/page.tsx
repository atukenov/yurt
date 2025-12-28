"use client";

import { useCartStore } from "@/store/cart";
import { ILocation, IMenuItem, ITopping } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";

export default function MenuPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { data: session } = useSession({
    required: true,
    onUnauthenticated() {
      router.push("/login");
    },
  });

  const [items, setItems] = useState<IMenuItem[]>([]);
  const [toppings, setToppings] = useState<ITopping[]>([]);
  const [locations, setLocations] = useState<ILocation[]>([]);
  const [selectedLocation, setSelectedLocation] = useState<string>("");
  const [category, setCategory] = useState<string>("");
  const [search, setSearch] = useState<string>("");
  const [selectedItem, setSelectedItem] = useState<IMenuItem | null>(null);
  const [selectedSize, setSelectedSize] = useState<
    "small" | "medium" | "large"
  >("medium");
  const [selectedToppings, setSelectedToppings] = useState<string[]>([]);
  const [specialInstructions, setSpecialInstructions] = useState("");
  const [quantity, setQuantity] = useState(1);
  const [loading, setLoading] = useState(true);

  const { addItem: addToCart, setLocation } = useCartStore();

  const categories = [
    "hot",
    "cold",
    "latte",
    "cappuccino",
    "espresso",
    "specialty",
  ];

  useEffect(() => {
    // Get search and location from URL params
    const urlSearch = searchParams.get("search") || "";
    const urlLocation = searchParams.get("location") || "";
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
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Category Filter */}
      <div className="mb-8 bg-white rounded-lg shadow p-4">
        <div className="flex gap-2 overflow-x-auto pb-2">
          <button
            onClick={() => setCategory("")}
            className={`px-4 py-2 rounded-full whitespace-nowrap transition ${
              category === ""
                ? "bg-amber-600 text-white"
                : "bg-gray-200 text-gray-700 hover:bg-gray-300"
            }`}
          >
            All
          </button>
          {categories.map((cat) => (
            <button
              key={cat}
              onClick={() => setCategory(cat)}
              className={`px-4 py-2 rounded-full whitespace-nowrap transition capitalize ${
                category === cat
                  ? "bg-amber-600 text-white"
                  : "bg-gray-200 text-gray-700 hover:bg-gray-300"
              }`}
            >
              {cat}
            </button>
          ))}
        </div>
      </div>

      {/* Menu Items Grid */}
      <div className="grid grid-cols-2 md:grid-cols-2 gap-6 mb-12">
        {items
          .filter(
            (item) =>
              item.name.toLowerCase().includes(search.toLowerCase()) ||
              item.description?.toLowerCase().includes(search.toLowerCase())
          )
          .map((item) => (
            <div
              key={item._id}
              className="bg-white rounded-lg shadow hover:shadow-lg transition overflow-hidden cursor-pointer"
              onClick={() => setSelectedItem(item)}
            >
              <div className="bg-linear-to-br from-amber-100 to-orange-100 h-48 flex items-center justify-center overflow-hidden">
                {item.image ? (
                  <img
                    src={item.image}
                    alt={item.name}
                    className="w-full h-full object-cover"
                  />
                ) : (
                  <span className="text-6xl">☕</span>
                )}
              </div>
              <div className="p-4">
                <h3 className="font-bold text-lg text-gray-900">{item.name}</h3>
                <p className="text-sm text-gray-600 mb-4">{item.description}</p>
                <div className="flex justify-between items-center">
                  <span className="text-lg sm:text-2xl font-bold text-amber-600">
                    ${item.basePrice.toFixed(2)}
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
                <h3 className="font-semibold text-gray-900 mb-3">Size</h3>
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
                        {s.size} (+${s.priceModifier.toFixed(2)})
                      </span>
                    </label>
                  ))}
                </div>
              </div>

              {/* Toppings Selection */}
              {toppings.length > 0 && (
                <div className="mb-6">
                  <h3 className="font-semibold text-gray-900 mb-3">
                    Add Toppings
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
                                  {topping.name} (+$
                                  {topping.price.toFixed(2)})
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
                  Special Instructions
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
                <h3 className="font-semibold text-gray-900 mb-3">Quantity</h3>
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
                    ${(calculatePrice() * quantity).toFixed(2)}
                  </p>
                </div>
                <button
                  onClick={handleAddToCart}
                  className="w-full px-4 py-3 bg-amber-600 text-white rounded-lg hover:bg-amber-700 transition font-semibold"
                >
                  Add to Cart
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
