"use client";

import { useLanguage } from "@/context/LanguageContext";
import { translations } from "@/lib/translations";
import { IOrder } from "@/types";
import { useSession } from "next-auth/react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export const dynamic = "force-dynamic";

export default function ArchivedOrdersPage() {
  const router = useRouter();
  const { data: session, status } = useSession();

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

  const [orders, setOrders] = useState<IOrder[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Wait for session to load
    if (status === "loading") {
      return;
    }

    // Check if user is authenticated
    if (status === "unauthenticated") {
      router.push("/login");
      return;
    }

    fetchOrders();
  }, [status, router]);

  const fetchOrders = async () => {
    try {
      const res = await fetch("/api/orders");
      if (res.ok) {
        const data = await res.json();
        setOrders(data.orders);
      }
    } catch (error) {
      console.error("Error fetching orders:", error);
    } finally {
      setLoading(false);
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case "completed":
        return "🎉";
      case "rejected":
        return "❌";
      default:
        return "📋";
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case "completed":
        return "bg-green-50 border-green-200 text-green-700";
      case "rejected":
        return "bg-red-50 border-red-200 text-red-700";
      default:
        return "bg-gray-50 border-gray-200 text-gray-700";
    }
  };

  if (status === "loading") {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12 text-center">
        <p className="text-gray-600">{t.processing}</p>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12 text-center">
        <p className="text-gray-600">{t.processing}</p>
      </div>
    );
  }

  const archivedOrders = orders.filter(
    (o) => !["pending", "accepted"].includes(o.status)
  );

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-3xl font-bold text-gray-900">{t.orderHistory}</h1>
        <div className="flex gap-2">
          <Link
            href="/orders"
            className="px-4 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-semibold"
          >
            {t.backToActiveOrders}
          </Link>
        </div>
      </div>

      {archivedOrders.length === 0 ? (
        <div className="bg-white rounded-lg shadow p-12 text-center">
          <p className="text-gray-600 mb-4">{t.noArchivedOrders}</p>
          <Link
            href="/orders"
            className="inline-block px-6 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700"
          >
            {t.viewActiveOrders}
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {archivedOrders.map((order) => (
            <div
              key={order._id}
              className={`rounded-lg shadow-sm hover:shadow-md transition border-l-4 p-4 cursor-pointer h-full flex flex-col ${
                order.status === "completed"
                  ? "border-l-green-500 bg-green-50"
                  : "border-l-red-500 bg-red-50"
              }`}
            >
              {/* Header with order number and status */}
              <div className="mb-3 pb-3 border-b">
                <div className="flex items-start justify-between gap-2 mb-2">
                  <div className="flex-1 min-w-0">
                    <p className="text-xs text-gray-600 uppercase tracking-wide">
                      {t.order}
                    </p>
                    <p className="font-bold text-gray-900 truncate">
                      {order.orderNumber}
                    </p>
                  </div>
                  <div className="text-2xl shrink-0">
                    {getStatusIcon(order.status)}
                  </div>
                </div>
                <span
                  className={`inline-block px-2 py-1 rounded-full text-xs font-semibold capitalize border ${getStatusColor(
                    order.status
                  )}`}
                >
                  {order.status}
                </span>
              </div>

              {/* Order info */}
              <div className="space-y-2 mb-4 flex-1">
                <div className="flex justify-between items-center">
                  <span className="text-sm text-gray-600">{t.price}:</span>
                  <span className="font-bold text-amber-600">
                    {order.totalPrice.toFixed(0)} ₸
                  </span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-sm text-gray-600">{t.items}:</span>
                  <span className="font-semibold text-gray-900">
                    {order.items?.length || 0}
                  </span>
                </div>
                {order.createdAt && (
                  <div className="flex justify-between items-center">
                    <span className="text-sm text-gray-600">{t.ordered}:</span>
                    <span className="font-semibold text-gray-900 text-xs">
                      {new Date(order.createdAt).toLocaleDateString()}
                    </span>
                  </div>
                )}
              </div>

              {/* Rejection reason if rejected */}
              {order.status === "rejected" && (
                <div className="mb-4 p-2 bg-red-100 rounded border border-red-300 text-xs">
                  <p className="font-semibold text-red-800 mb-1">Reason:</p>
                  <p className="text-red-700 line-clamp-2">
                    {order.rejectionReason}
                    {order.rejectionComment && ` - ${order.rejectionComment}`}
                  </p>
                </div>
              )}

              {/* Details button */}
              <div className="flex gap-2 pt-3 border-t">
                <Link
                  href={`/orders/${order._id}`}
                  className="flex-1 px-3 py-2 bg-white border-2 border-gray-300 text-gray-700 rounded-lg hover:border-amber-500 hover:text-amber-600 transition font-semibold text-sm text-center"
                >
                  {t.orderDetails}
                </Link>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
