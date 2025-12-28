"use client";

import { useCartStore } from "@/store/cart";
import { useSession } from "next-auth/react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export default function CartPage() {
  const router = useRouter();
  const { data: session } = useSession({
    required: true,
    onUnauthenticated() {
      router.push("/login");
    },
  });

  const [mounted, setMounted] = useState(false);
  const { items, getTotalPrice, removeItem, updateItem, clearCart } =
    useCartStore();

  useEffect(() => {
    setMounted(true);
  }, []);

  if (!mounted) return null;

  if (items.length === 0) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        <div className="text-center">
          <h1 className="text-3xl font-bold text-gray-900 mb-4">
            Your Cart is Empty
          </h1>
          <p className="text-gray-600 mb-8">
            Add some delicious coffee to get started!
          </p>
          <Link
            href="/menu"
            className="inline-block px-6 py-3 bg-amber-600 text-white rounded-lg hover:bg-amber-700 transition font-semibold"
          >
            Continue Shopping
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <h1 className="text-3xl font-bold text-gray-900 mb-8">Your Cart</h1>

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
                    Size: {item.size.toUpperCase()}
                  </p>
                  {item.toppings.length > 0 && (
                    <p className="text-sm text-gray-600">
                      Toppings: {item.toppings.map((t) => t.name).join(", ")}
                    </p>
                  )}
                  {item.specialInstructions && (
                    <p className="text-sm text-gray-600">
                      Notes: {item.specialInstructions}
                    </p>
                  )}
                </div>
                <button
                  onClick={() => removeItem(item.id)}
                  className="text-red-600 hover:text-red-700 font-semibold"
                >
                  Remove
                </button>
              </div>

              <div className="flex justify-between items-center">
                <div className="flex items-center gap-3">
                  <button
                    onClick={() =>
                      updateItem(item.id, {
                        quantity: Math.max(1, item.quantity - 1),
                      })
                    }
                    className="px-3 py-1 border border-gray-300 rounded hover:bg-gray-100"
                  >
                    −
                  </button>
                  <span className="text-lg font-semibold">{item.quantity}</span>
                  <button
                    onClick={() =>
                      updateItem(item.id, { quantity: item.quantity + 1 })
                    }
                    className="px-3 py-1 border border-gray-300 rounded hover:bg-gray-100"
                  >
                    +
                  </button>
                </div>
                <span className="text-xl font-bold text-amber-600">
                  ${(item.price * item.quantity).toFixed(2)}
                </span>
              </div>
            </div>
          ))}
        </div>

        {/* Order Summary */}
        <div className="bg-white rounded-lg shadow p-6 h-fit">
          <h2 className="text-lg font-bold text-gray-900 mb-6">
            Order Summary
          </h2>

          <div className="space-y-4 mb-6">
            <div className="flex justify-between">
              <span className="text-gray-700">Subtotal</span>
              <span className="font-semibold">
                ${getTotalPrice().toFixed(2)}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-700">Delivery Fee</span>
              <span className="font-semibold">$2.00</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-700">Tax</span>
              <span className="font-semibold">
                ${((getTotalPrice() + 2) * 0.1).toFixed(2)}
              </span>
            </div>
          </div>

          <div className="border-t pt-4 mb-6">
            <div className="flex justify-between mb-4">
              <span className="text-lg font-bold text-gray-900">Total</span>
              <span className="text-lg font-bold text-amber-600">
                $
                {(getTotalPrice() + 2 + (getTotalPrice() + 2) * 0.1).toFixed(2)}
              </span>
            </div>
          </div>

          <div className="space-y-3">
            <Link
              href="/checkout"
              className="block w-full px-4 py-3 bg-amber-600 text-white rounded-lg hover:bg-amber-700 transition font-semibold text-center"
            >
              Proceed to Checkout
            </Link>
            <Link
              href="/menu"
              className="block w-full px-4 py-3 border border-gray-300 text-gray-700 rounded-lg hover:bg-gray-50 transition font-semibold text-center"
            >
              Continue Shopping
            </Link>
            <button
              onClick={() => clearCart()}
              className="w-full px-4 py-3 text-red-600 hover:text-red-700 font-semibold"
            >
              Clear Cart
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
