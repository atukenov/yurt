"use client";

import { withErrorBoundary } from "@/components/ErrorBoundary";
import { OrderGridSkeleton } from "@/components/SkeletonLoaders";
import { useSocket } from "@/components/SocketProvider";
import { useLanguage } from "@/context/LanguageContext";
import { apiClient } from "@/lib/api-client";
import { translations, type Language } from "@/lib/translations";
import { IOrder } from "@/types";
import { useSession } from "next-auth/react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export const dynamic = "force-dynamic";

function OrdersPageContent() {
  const router = useRouter();
  const { data: session, status } = useSession();
  const { isConnected, isAvailable, orderEvents } = useSocket();

  // Safely access language context with fallback
  let language: Language = "ru";
  let t = translations.ru.client;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.client || translations.ru.client;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.ru.client;
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

    // Setup Socket.io listeners for real-time updates
    if (orderEvents.orderCreated) {
      orderEvents.orderCreated((newOrder: IOrder) => {
        console.log("[Customer] New order:", newOrder);
        setOrders((prev) => [newOrder, ...prev]);
      });
    }

    if (orderEvents.orderStatusChanged) {
      orderEvents.orderStatusChanged(
        (data: { orderId: string; status: string }) => {
          console.log("[Customer] Order status changed:", data);
          setOrders((prev) =>
            prev.map((order) =>
              order._id === data.orderId
                ? { ...order, status: data.status as any }
                : order
            )
          );
        }
      );
    }

    // Fallback to polling every 10 seconds if Socket.io is not connected
    const pollInterval = setInterval(() => {
      if (!isConnected) {
        console.log("[Customer] WebSocket not connected, using polling...");
        fetchOrders();
      }
    }, 10000);

    return () => clearInterval(pollInterval);
  }, [status, router]);

  const fetchOrders = async () => {
    try {
      const data = await apiClient.get("/orders");
      setOrders(data.orders);
    } catch (error) {
      console.error("Error fetching orders:", error);
    } finally {
      setLoading(false);
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case "pending":
        return "⏳";
      case "accepted":
        return "✅";
      case "completed":
        return "🎉";
      case "rejected":
        return "❌";
      default:
        return "📋";
    }
  };

  const getStatusLabel = (status: string) => {
    const statusLabels = t.statusLabels || {
      pending: "Pending",
      accepted: "Accepted",
      completed: "Completed",
      rejected: "Rejected",
      cancelled: "Cancelled",
    };
    return statusLabels[status as keyof typeof statusLabels] || status;
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case "pending":
        return "bg-yellow-50 border-yellow-200 text-yellow-700";
      case "accepted":
        return "bg-blue-50 border-blue-200 text-blue-700";
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
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        <OrderGridSkeleton count={3} />
      </div>
    );
  }

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-12">
        <OrderGridSkeleton count={3} />
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="flex justify-between items-center mb-8">
        <h1 className="text-2xl font-bold text-gray-900">{t.activeOrders}</h1>
        <div className="flex gap-2">
          <Link
            href="/orders/archived"
            className="px-4 py-2 bg-gray-600 text-white rounded-lg hover:bg-gray-700 transition font-semibold"
          >
            {t.history}
          </Link>
          <Link
            href="/menu"
            className="px-4 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition font-semibold"
          >
            {t.newOrder}
          </Link>
        </div>
      </div>

      {/* Filter orders to show only pending and accepted */}
      {orders.filter((o) => ["pending", "accepted"].includes(o.status))
        .length === 0 ? (
        <div className="bg-white rounded-lg shadow p-12 text-center">
          <p className="text-gray-600 mb-4">{t.noActiveOrders}</p>
          <Link
            href="/menu"
            className="inline-block px-6 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700"
          >
            {t.startOrdering}
          </Link>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {orders
            .filter((o) => ["pending", "accepted"].includes(o.status))
            .map((order) => (
              <div
                key={order._id}
                className={`rounded-lg shadow-sm hover:shadow-md transition border-l-4 p-4 cursor-pointer h-full flex flex-col ${
                  order.status === "pending"
                    ? "border-l-yellow-500 bg-yellow-50"
                    : order.status === "accepted"
                      ? "border-l-blue-500 bg-blue-50"
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
                    {getStatusLabel(order.status)}
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
                  {order.estimatedPrepTime && (
                    <div className="flex justify-between items-center">
                      <span className="text-sm text-gray-600">
                        {t.estimatedTime}:
                      </span>
                      <span className="font-semibold text-gray-900">
                        {order.estimatedPrepTime} {t.min}
                      </span>
                    </div>
                  )}
                </div>

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

const OrdersPage = withErrorBoundary(OrdersPageContent, "Orders");
export default OrdersPage;
