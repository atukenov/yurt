"use client";

import { withErrorBoundary } from "@/components/ErrorBoundary";
import { FormError } from "@/components/FormFields";
import { CheckoutSkeleton } from "@/components/SkeletonLoaders";
import { errorLogger } from "@/lib/logger";
import { CheckoutSchema, validateFormData } from "@/lib/validation";
import { useCartStore } from "@/store/cart";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

function CheckoutPageContent() {
  const router = useRouter();
  const { data: session } = useSession({
    required: true,
    onUnauthenticated() {
      router.push("/login");
    },
  });

  const [mounted, setMounted] = useState(false);
  const [paymentMethod, setPaymentMethod] = useState<
    "cash" | "card" | "stripe"
  >("cash");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

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
      setError("You must be logged in to place an order.");
      return;
    }

    if (!locationId) {
      setError("Please select a location");
      return;
    }

    // Validate checkout input
    const validation = validateFormData(CheckoutSchema, {
      locationId,
      paymentMethod,
      notes: notes || undefined,
    });
    if (!validation.success) {
      setError(
        Object.values(validation.errors || {})[0] || "Invalid checkout data"
      );
      errorLogger.warn("Checkout validation failed", {
        locationId,
        paymentMethod,
      });
      return;
    }

    setLoading(true);
    setError("");

    try {
      const total = getTotalPrice() + 2 + (getTotalPrice() + 2) * 0.1;

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
        const message = data.error || "Failed to create order";
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
      const message =
        err instanceof Error
          ? err.message
          : "An error occurred. Please try again.";
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
  const deliveryFee = 2;
  const tax = (subtotal + deliveryFee) * 0.1;
  const total = subtotal + deliveryFee + tax;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-gray-900 mb-8">Checkout</h1>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* Order Details */}
        <div className="lg:col-span-2">
          <div className="bg-white rounded-lg shadow p-6 mb-6">
            <h2 className="text-xl font-bold text-gray-900 mb-4">
              Order Items
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
                    ${(item.price * item.quantity).toFixed(2)}
                  </p>
                </div>
              ))}
            </div>
          </div>

          {/* Payment Method */}
          <div className="bg-white rounded-lg shadow p-6 mb-6">
            <h2 className="text-xl font-bold text-gray-900 mb-4">
              Payment Method
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
                <span className="ml-3 text-gray-700">Cash on Pickup</span>
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
                <span className="ml-3 text-gray-700">Debit/Credit Card</span>
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
                <span className="ml-3 text-gray-700">Pay with Stripe</span>
              </label>
            </div>
          </div>

          {/* Special Notes */}
          <div className="bg-white rounded-lg shadow p-6">
            <h2 className="text-xl font-bold text-gray-900 mb-4">
              Special Notes
            </h2>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Any special requests or notes for the barista?"
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:border-amber-500 focus:outline-none"
              rows={4}
            />
          </div>
        </div>

        {/* Order Summary */}
        <div className="bg-white rounded-lg shadow p-6 h-fit">
          <h2 className="text-xl font-bold text-gray-900 mb-6">
            Order Summary
          </h2>

          <div className="space-y-3 mb-6">
            <div className="flex justify-between">
              <span className="text-gray-700">Subtotal</span>
              <span className="font-semibold">${subtotal.toFixed(2)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-700">Delivery Fee</span>
              <span className="font-semibold">${deliveryFee.toFixed(2)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-700">Tax (10%)</span>
              <span className="font-semibold">${tax.toFixed(2)}</span>
            </div>
          </div>

          <div className="border-t pt-4 mb-6">
            <div className="flex justify-between">
              <span className="text-lg font-bold text-gray-900">Total</span>
              <span className="text-lg font-bold text-amber-600">
                ${total.toFixed(2)}
              </span>
            </div>
          </div>

          {error && <FormError error={error} />}

          <button
            onClick={handleSubmitOrder}
            disabled={loading}
            className="w-full px-4 py-3 bg-amber-600 text-white rounded-lg hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed transition font-semibold"
          >
            {loading ? "Processing..." : "Place Order"}
          </button>

          <p className="text-xs text-gray-600 mt-4 text-center">
            You'll receive order updates via email and SMS
          </p>
        </div>
      </div>
    </div>
  );
}

const CheckoutPage = withErrorBoundary(CheckoutPageContent, "Checkout");
export default CheckoutPage;
