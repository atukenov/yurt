"use client";

import { DisplayReview, ReviewDisplay } from "@/components/ReviewDisplay";
import { ReviewForm } from "@/components/ReviewForm";
import { OrderDetailsSkeleton } from "@/components/SkeletonLoaders";
import { useSocket } from "@/components/SocketProvider";
import { useLanguage } from "@/context/LanguageContext";
import { apiClient } from "@/lib/api-client";
import { translations, type Language } from "@/lib/translations";
import { IOrder } from "@/types";
import { useSession } from "next-auth/react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useEffect, useState } from "react";

export const dynamic = "force-dynamic";

export default function OrderDetailsPage() {
  const params = useParams();
  const orderId = params?.id as string;
  const router = useRouter();
  const { data: session, status } = useSession();
  const { isConnected, isAvailable, orderEvents } = useSocket();

  // Safely access language context with fallback
  let language: Language = "ru";
  let t = translations.ru.client;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = (translations[language]?.client ||
      translations.en.client) as typeof translations.en.client;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.client;
  }

  const [order, setOrder] = useState<IOrder | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reviews, setReviews] = useState<DisplayReview[]>([]);
  const [reviewingItemId, setReviewingItemId] = useState<string | null>(null);

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

    // Don't fetch if orderId is not available
    if (!orderId) {
      return;
    }

    fetchOrder();

    // Setup Socket.io listeners for real-time updates
    if (orderEvents.orderStatusChanged) {
      orderEvents.orderStatusChanged(
        (data: { orderId: string; status: string }) => {
          if (data.orderId === orderId) {
            console.log("[Order Details] Status changed:", data.status);
            setOrder((prev) =>
              prev ? { ...prev, status: data.status as any } : null
            );
          }
        }
      );
    }

    // Fallback to polling every 10 seconds
    const pollInterval = setInterval(() => {
      fetchOrder();
    }, 10000);

    return () => clearInterval(pollInterval);
  }, [status, router, orderId]);

  const fetchOrder = async () => {
    try {
      const data = await apiClient.get(`/orders/${orderId}`);
      setOrder(data.order);
      setError(null);

      // Fetch reviews for this order
      try {
        const reviewsData = await apiClient.get(`/reviews?orderId=${orderId}`);
        setReviews(reviewsData.reviews || []);
      } catch {
        // Reviews fetch failure is non-critical
      }
    } catch (err) {
      if ((err as any).status === 404) {
        setError("Order not found");
      } else {
        console.error("Error fetching order:", err);
        setError("Failed to load order");
      }
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

  if (loading) {
    return <OrderDetailsSkeleton />;
  }

  if (error || !order) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <div className="text-center">
          <p className="text-xl font-bold text-gray-900 mb-4">
            {error || t.orderNotFound}
          </p>
          <Link
            href="/orders"
            className="inline-block px-6 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700"
          >
            {t.backToOrders}
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 py-8 px-4 pb-24 sm:pb-8">
      <div className="max-w-2xl mx-auto">
        {/* Header */}
        <div className="mb-6">
          <Link
            href="/orders"
            className="text-amber-600 hover:text-amber-700 font-semibold mb-4 inline-block"
          >
            ← {t.backToOrders}
          </Link>
        </div>

        {/* Order Card */}
        <div className="bg-white rounded-lg shadow-lg p-6">
          {/* Header with order number and status */}
          <div className="border-b pb-6 mb-6">
            <div className="flex items-start justify-between gap-4 mb-4">
              <div>
                <p className="text-sm text-gray-600 uppercase tracking-wide mb-2">
                  {t.orderNumber}
                </p>
                <p className="text-3xl font-bold text-gray-900">
                  {order.orderNumber}
                </p>
              </div>
              <div className="text-5xl">{getStatusIcon(order.status)}</div>
            </div>
            <span
              className={`inline-block px-3 py-1 rounded-full text-sm font-semibold capitalize border ${getStatusColor(
                order.status
              )}`}
            >
              {getStatusLabel(order.status)}
            </span>
          </div>

          {/* Order Summary */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
            <div className="bg-gray-50 p-4 rounded-lg">
              <p className="text-xs text-gray-600 uppercase mb-1">{t.total}</p>
              <p className="text-2xl font-bold text-amber-600">
                {order.totalPrice?.toFixed(0) || "0"} ₸
              </p>
            </div>
            <div className="bg-gray-50 p-4 rounded-lg">
              <p className="text-xs text-gray-600 uppercase mb-1">
                {t.itemsOrdered}
              </p>
              <p className="text-2xl font-bold text-gray-900">
                {order.items?.length || 0}
              </p>
            </div>
            {order.estimatedPrepTime && (
              <div className="bg-gray-50 p-4 rounded-lg">
                <p className="text-xs text-gray-600 uppercase mb-1">
                  {t.estimatedTime}
                </p>
                <p className="text-2xl font-bold text-gray-900">
                  {order.estimatedPrepTime}
                  {t.min}
                </p>
              </div>
            )}
            {order.location && typeof order.location !== "string" && (
              <div className="bg-gray-50 p-4 rounded-lg">
                <p className="text-xs text-gray-600 uppercase mb-1">
                  {t.location}
                </p>
                <p className="text-sm font-bold text-gray-900">
                  {order.location.name || "Unknown"}
                </p>
              </div>
            )}
          </div>

          {/* Order Items */}
          <div className="mb-6">
            <h2 className="text-lg font-bold text-gray-900 mb-4">
              {t.itemsOrdered}
            </h2>
            <div className="space-y-3">
              {order.items && order.items.length > 0 ? (
                order.items.map((item: any, index: number) => {
                  const itemName =
                    typeof item.menuItem === "string"
                      ? "Coffee Item"
                      : item.menuItem?.name || "Coffee Item";

                  return (
                    <div
                      key={index}
                      className="flex justify-between items-start p-3 bg-gray-50 rounded-lg"
                    >
                      <div className="flex-1">
                        <p className="font-semibold text-gray-900">
                          {itemName}
                        </p>
                        {item.size && (
                          <p className="text-sm text-gray-600">
                            {t.selectSize}: {item.size}
                          </p>
                        )}
                        {item.toppings && item.toppings.length > 0 && (
                          <p className="text-sm text-gray-600">
                            {t.addToppings}: {item.toppings.join(", ")}
                          </p>
                        )}
                        {item.specialInstructions && (
                          <p className="text-sm text-gray-600 italic">
                            {t.notes}: {item.specialInstructions}
                          </p>
                        )}
                      </div>
                      <div className="text-right ml-4 shrink-0">
                        <p className="font-semibold text-gray-900">
                          {item.priceAtOrder?.toFixed(0) || "0"} ₸
                        </p>
                        <p className="text-sm text-gray-600">
                          x{item.quantity || 1}
                        </p>
                      </div>
                    </div>
                  );
                })
              ) : (
                <p className="text-gray-600">{t.noItemsMessage}</p>
              )}
            </div>
          </div>

          {/* Order Details */}
          <div className="border-t pt-6">
            <h2 className="text-lg font-bold text-gray-900 mb-4">
              {t.orderDetails}
            </h2>
            <div className="space-y-3">
              <div className="flex justify-between">
                <span className="text-gray-600">{t.orderDate}:</span>
                <span className="font-semibold text-gray-900">
                  {order.createdAt
                    ? new Date(order.createdAt).toLocaleString()
                    : "N/A"}
                </span>
              </div>
              {order.paymentMethod && (
                <div className="flex justify-between">
                  <span className="text-gray-600">{t.paymentMethodLabel}:</span>
                  <span className="font-semibold text-gray-900 capitalize">
                    {order.paymentMethod}
                  </span>
                </div>
              )}
              {order.status === "rejected" && order.rejectionReason && (
                <div className="flex justify-between">
                  <span className="text-gray-600">{t.rejectionReason}:</span>
                  <span className="font-semibold text-red-600">
                    {order.rejectionReason}
                  </span>
                </div>
              )}
            </div>
          </div>

          {/* Real-time Connection Status */}
          {isAvailable && (
            <div className="mt-6 p-3 bg-green-50 border border-green-200 rounded-lg">
              <p className="text-sm text-green-700">
                ✅ Real-time updates enabled. This page will update
                automatically when your order status changes.
              </p>
            </div>
          )}
        </div>

        {/* Reviews Section */}
        {order && order.status === "completed" && (
          <div className="mt-8">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">
              {t.reviews}
            </h2>

            {/* Review Forms for Each Item */}
            <div className="grid grid-cols-1 gap-6 mb-8">
              {order.items?.map((item) => {
                const menuItem =
                  typeof item.menuItem === "object" ? item.menuItem : null;
                const menuItemId =
                  typeof item.menuItem === "string"
                    ? item.menuItem
                    : item.menuItem?._id;
                const menuItemName =
                  menuItem?.name ||
                  (typeof item.menuItem === "object"
                    ? item.menuItem.name
                    : "Unknown Item");

                const itemReview = reviews.find(
                  (r) => r.menuItem._id === menuItemId
                );
                const hasReviewedItem = itemReview !== undefined;

                return (
                  <div
                    key={menuItemId}
                    className="bg-white rounded-lg shadow p-6"
                  >
                    <div className="mb-4">
                      <h3 className="font-bold text-lg text-gray-900">
                        {menuItemName}
                      </h3>
                      {hasReviewedItem && itemReview && (
                        <div className="flex items-center gap-2 mt-2">
                          <div className="flex gap-0.5">
                            {[1, 2, 3, 4, 5].map((star) => (
                              <span
                                key={star}
                                className={
                                  star <= itemReview.rating
                                    ? "text-yellow-400"
                                    : "text-gray-300"
                                }
                              >
                                ★
                              </span>
                            ))}
                          </div>
                          <span className="text-sm text-gray-600">
                            Your rating: {itemReview.rating}/5
                          </span>
                        </div>
                      )}
                    </div>

                    {!hasReviewedItem ? (
                      <>
                        {reviewingItemId !== menuItemId ? (
                          <button
                            onClick={() => setReviewingItemId(menuItemId)}
                            className="px-4 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 font-semibold transition"
                          >
                            {t.leaveReview}
                          </button>
                        ) : (
                          <ReviewForm
                            orderId={orderId}
                            menuItem={
                              {
                                _id: menuItemId,
                                name: menuItemName,
                              } as any
                            }
                            onSubmit={() => {
                              setReviewingItemId(null);
                              fetchOrder();
                            }}
                            onCancel={() => setReviewingItemId(null)}
                          />
                        )}
                      </>
                    ) : (
                      <div className="bg-green-50 border border-green-200 rounded-lg p-4">
                        <p className="text-green-700 text-sm">
                          ✓ {t.reviewAlreadyLeft}
                        </p>
                        {itemReview.comment && (
                          <p className="mt-2 text-gray-700">
                            "{itemReview.comment}"
                          </p>
                        )}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>

            {/* All Reviews for This Order */}
            {reviews.length > 0 && (
              <div className="bg-white rounded-lg shadow p-6">
                <h3 className="font-bold text-lg text-gray-900 mb-4">
                  {t.reviews}
                </h3>
                <ReviewDisplay reviews={reviews} />
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
