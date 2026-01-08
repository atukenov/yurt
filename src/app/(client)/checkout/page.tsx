"use client";

import { withErrorBoundary } from "@/components/ErrorBoundary";
import { FormError } from "@/components/FormFields";
import { LocationAvailabilityDisplay } from "@/components/LocationAvailabilityDisplay";
import { CheckoutSkeleton } from "@/components/SkeletonLoaders";
import { useLanguage } from "@/context/LanguageContext";
import { errorLogger } from "@/lib/logger";
import { translations } from "@/lib/translations";
import { CheckoutSchema, validateFormData } from "@/lib/validation";
import { useCartStore } from "@/store/cart";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export const dynamic = "force-dynamic";

function CheckoutPageContent() {
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
  const [paymentMethod, setPaymentMethod] = useState<
    "cash" | "card" | "stripe"
  >("cash");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [locationAvailable, setLocationAvailable] = useState(true);

  const { items, locationId, getTotalPrice, clearCart } = useCartStore();

  useEffect(() => {
    setMounted(true);
  }, []);

  useEffect(() => {
    if (mounted && items.length === 0) {
      router.push("/cart");
    }
  }, [mounted, items.length, router]);

  if (!mounted) {
    return <CheckoutSkeleton />;
  }

  const handleSubmitOrder = async () => {
    if (!session?.user?.id) {
      setError(t.mustBeLoggedIn);
      return;
    }

    if (!locationId) {
      setError(t.selectLocationCheckoutError);
      return;
    }

    // Check location availability first
    if (!locationAvailable) {
      setError(t.locationClosedError);
      return;
    }

    // Validate checkout input
    const validation = validateFormData(CheckoutSchema, {
      locationId,
      paymentMethod,
      notes: notes || undefined,
    });
    if (!validation.success) {
      setError(Object.values(validation.errors || {})[0] || t.invalidCheckout);
      errorLogger.warn("Checkout validation failed", {
        locationId,
        paymentMethod,
      });
      return;
    }

    setLoading(true);
    setError("");

    try {
      const total = getTotalPrice() + 200 + (getTotalPrice() + 200) * 0.1;

      const res = await fetch("/api/orders", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          locationId,
          items: items.map((item) => ({
            menuItemId: item.menuItemId,
            quantity: item.quantity,
            size: item.size,
            toppings: item.toppings.map((t) => t.id),
            specialInstructions: item.specialInstructions,
          })),
          totalPrice: total,
          paymentMethod,
          notes,
        }),
      });

      if (!res.ok) {
        const data = await res.json();
        const message = data.error || t.failedCreateOrder;
        setError(message);
        errorLogger.warn(
          "Order creation failed",
          { locationId, paymentMethod },
          new Error(message)
        );
        return;
      }

      const data = await res.json();
      errorLogger.info("Order placed successfully", {
        orderId: data.order.id,
        total,
      });
      clearCart();
      router.push(`/orders/${data.order.id}?status=confirmed`);
    } catch (err) {
      const message = err instanceof Error ? err.message : t.orderUpdatesEmail;
      setError(message);
      errorLogger.error(
        "Checkout error",
        { locationId },
        err instanceof Error ? err : new Error(message)
      );
    } finally {
      setLoading(false);
    }
  };

  const subtotal = getTotalPrice();
  const deliveryFee = 200;
  const tax = (subtotal + deliveryFee) * 0.1;
  const total = subtotal + deliveryFee + tax;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-gray-900 mb-8">{t.checkout}</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Order Details */}
        <div className="lg:col-span-2">
          {/* Location Availability Status */}
          {locationId && (
            <div className="mb-6">
              <LocationAvailabilityDisplay
                locationId={locationId}
                onAvailabilityChange={setLocationAvailable}
              />
            </div>
          )}

          <div className="bg-white rounded-lg shadow p-6 mb-6">
            <h2 className="text-xl font-bold text-gray-900 mb-4">
              {t.orderItemsInCheckout}
            </h2>
            <div className="space-y-4">
              {items.map((item) => (
                <div
                  key={item.id}
                  className="flex justify-between py-2 border-b"
                >
                  <div>
                    <p className="font-medium text-gray-900">
                      {item.name} x{item.quantity}
                    </p>
                    <p className="text-sm text-gray-600">{item.size}</p>
                  </div>
                  <p className="font-semibold text-gray-900">
                    {(item.price * item.quantity).toFixed(0)} ₸
                  </p>
                </div>
              ))}
            </div>
          </div>

          {/* Payment Method */}
          <div className="bg-white rounded-lg shadow p-6 mb-6">
            <h2 className="text-xl font-bold text-gray-900 mb-4">
              {t.paymentMethodTitle}
            </h2>
            <div className="space-y-3">
              <label className="flex items-center">
                <input
                  type="radio"
                  name="payment"
                  value="cash"
                  checked={paymentMethod === "cash"}
                  onChange={(e) =>
                    setPaymentMethod(
                      e.target.value as "cash" | "card" | "stripe"
                    )
                  }
                  className="w-4 h-4"
                />
                <span className="ml-3 text-gray-700">{t.cashOnPickup}</span>
              </label>
              <label className="flex items-center">
                <input
                  type="radio"
                  name="payment"
                  value="card"
                  checked={paymentMethod === "card"}
                  onChange={(e) =>
                    setPaymentMethod(
                      e.target.value as "cash" | "card" | "stripe"
                    )
                  }
                  className="w-4 h-4"
                />
                <span className="ml-3 text-gray-700">{t.debitCreditCard}</span>
              </label>
              <label className="flex items-center">
                <input
                  type="radio"
                  name="payment"
                  value="stripe"
                  checked={paymentMethod === "stripe"}
                  onChange={(e) =>
                    setPaymentMethod(
                      e.target.value as "cash" | "card" | "stripe"
                    )
                  }
                  className="w-4 h-4"
                />
                <span className="ml-3 text-gray-700">{t.payWithStripe}</span>
              </label>
            </div>
          </div>

          {/* Special Notes */}
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-xl font-bold text-gray-900 mb-4">
              {t.specialNotes}
            </h2>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder={t.notesPlaceholder}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:border-amber-500 focus:outline-none"
              rows={4}
            />
          </div>
        </div>

        {/* Order Summary */}
        <div className="bg-white rounded-lg shadow p-6 h-fit">
          <h2 className="text-xl font-bold text-gray-900 mb-6">
            {t.orderSummary}
          </h2>

          <div className="space-y-3 mb-6">
            <div className="flex justify-between">
              <span className="text-gray-700">{t.subtotal}</span>
              <span className="font-semibold">{subtotal.toFixed(0)} ₸</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-700">{t.deliveryFee}</span>
              <span className="font-semibold">{deliveryFee.toFixed(0)} ₸</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-700">{t.tax} (10%)</span>
              <span className="font-semibold">{tax.toFixed(0)} ₸</span>
            </div>
          </div>

          <div className="border-t pt-4 mb-6">
            <div className="flex justify-between">
              <span className="text-lg font-bold text-gray-900">{t.total}</span>
              <span className="text-lg font-bold text-amber-600">
                {total.toFixed(0)} ₸
              </span>
            </div>
          </div>

          {error && <FormError error={error} />}

          <button
            onClick={handleSubmitOrder}
            disabled={loading || !locationAvailable}
            className="w-full px-4 py-3 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed transition font-semibold"
          >
            {loading ? t.processing : t.placeOrder}
          </button>

          <p className="text-xs text-gray-600 mt-4 text-center">
            {t.orderUpdatesEmail}
          </p>
        </div>
      </div>
    </div>
  );
}

const CheckoutPage = withErrorBoundary(CheckoutPageContent, "Checkout");
export default CheckoutPage;
