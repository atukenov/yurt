"use client";

import { QuantitySelector } from "@/components/QuantitySelector";
import { useLanguage } from "@/context/LanguageContext";
import { translations } from "@/lib/translations";
import { useCartStore } from "@/store/cart";
import { useSession } from "next-auth/react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export const dynamic = "force-dynamic";

export default function CartPage() {
  const router = useRouter();
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

  const [mounted, setMounted] = useState(false);
  const {
    items,
    locationId,
    getTotalPrice,
    removeItem,
    updateItem,
    clearCart,
    isLocationSelected,
  } = useCartStore();

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) return null;

  if (items.length === 0) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        <div className="text-center">
          <h1 className="text-3xl font-bold text-gray-900 mb-4">
            {t.yourCartIsEmpty}
          </h1>
          <p className="text-gray-600 mb-8">{t.addCoffeeToGetStarted}</p>
          <Link
            href="/menu"
            className="inline-block px-6 py-3 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-semibold"
          >
            {t.continueShopping}
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 pb-20 md:pb-8">
      <h1 className="text-3xl font-bold text-gray-900 mb-8">{t.yourCart}</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Cart Items */}
        <div className="lg:col-span-2 space-y-4">
          {items.map((item) => (
            <div key={item.id} className="bg-white rounded-lg shadow p-6">
              <div className="flex justify-between items-start mb-4">
                <div>
                  <h3 className="text-lg font-bold text-gray-900">
                    {item.name}
                  </h3>
                  <p className="text-sm text-gray-600">
                    {t.size}: {item.size.toUpperCase()}
                  </p>
                  {item.toppings.length > 0 && (
                    <p className="text-sm text-gray-600">
                      {t.toppings}:{" "}
                      {item.toppings.map((t) => t.name).join(", ")}
                    </p>
                  )}
                  {item.specialInstructions && (
                    <p className="text-sm text-gray-600">
                      {t.notes}: {item.specialInstructions}
                    </p>
                  )}
                </div>
              </div>

              <div className="flex justify-between items-center">
                <QuantitySelector
                  quantity={item.quantity}
                  onQuantityChange={(newQuantity) =>
                    updateItem(item.id, { quantity: newQuantity })
                  }
                  onDelete={() => removeItem(item.id)}
                />
                <span className="text-xl font-bold text-amber-600">
                  {(item.price * item.quantity).toFixed(0)} ₸
                </span>
              </div>
            </div>
          ))}
        </div>

        {/* Order Summary */}
        <div className="bg-white rounded-lg shadow p-6 h-fit">
          <h2 className="text-lg font-bold text-gray-900 mb-6">
            {t.orderSummary}
          </h2>

          <div className="space-y-4 mb-6">
            <div className="flex justify-between">
              <span className="text-gray-700">{t.subtotal}</span>
              <span className="font-semibold">
                {getTotalPrice().toFixed(0)} ₸
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-700">{t.tax}</span>
              <span className="font-semibold">
                {(getTotalPrice() * 0.1).toFixed(0)} ₸
              </span>
            </div>
          </div>

          <div className="border-t pt-4 mb-6">
            <div className="flex justify-between mb-4">
              <span className="text-lg font-bold text-gray-900">{t.total}</span>
              <span className="text-lg font-bold text-amber-600">
                {(getTotalPrice() + getTotalPrice() * 0.1).toFixed(0)} ₸
              </span>
            </div>
          </div>

          <div className="space-y-3">
            {!isLocationSelected() && (
              <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-4">
                <p className="text-red-700 text-sm font-semibold">
                  {t.selectLocationCheckout}
                </p>
              </div>
            )}
            <Link
              href="/checkout"
              className={`block w-full px-4 py-3 rounded-lg transition font-semibold text-center ${
                isLocationSelected()
                  ? "bg-[#ffd119] text-black hover:bg-amber-700"
                  : "bg-gray-300 text-gray-500 cursor-not-allowed"
              }`}
              onClick={(e) => {
                if (!isLocationSelected()) {
                  e.preventDefault();
                }
              }}
            >
              {t.proceedCheckout}
            </Link>
            <Link
              href="/menu"
              className="block w-full px-4 py-3 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 transition font-semibold text-center"
            >
              {t.continueShopping}
            </Link>
            <button
              onClick={() => clearCart()}
              className="w-full px-4 py-3 text-red-600 hover:text-red-700 font-semibold"
            >
              {t.clearCart}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
