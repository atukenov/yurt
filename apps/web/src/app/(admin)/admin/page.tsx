"use client";

import { OrderColumnSection } from "@/components/admin/OrderColumnSection";
import { OrderDetailsSidebar } from "@/components/admin/OrderDetailsSidebar";
import { StatCard } from "@/components/admin/StatCard";
import { withErrorBoundary } from "@/components/ErrorBoundary";
import { OrderGridSkeleton } from "@/components/SkeletonLoaders";
import { useSocket } from "@/components/SocketProvider";
import { useLanguage } from "@/context/LanguageContext";
import { apiClient } from "@/lib/api-client";
import { errorLogger } from "@/lib/logger";
import { translations, type Language } from "@/lib/translations";
import { IOrder } from "@/types";
import { format, subDays } from "date-fns";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";

// Prevent static export for this dynamic page
export const dynamic = "force-dynamic";

interface Location {
  _id: string;
  name: string;
}

interface HighlightedOrder {
  [key: string]: boolean;
}

function AdminDashboardContent() {
  const router = useRouter();
  const { data: session, status } = useSession();
  const { isConnected, orderEvents } = useSocket();

  // Safely access language context with fallback
  let language: Language = "ru";
  let t = translations.ru.admin;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.admin || translations.ru.admin;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.ru.admin;
  }

  const [orders, setOrders] = useState<IOrder[]>([]);
  const [allOrders, setAllOrders] = useState<IOrder[]>([]);
  const [selectedOrder, setSelectedOrder] = useState<IOrder | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<"active" | "completed">("active");
  const [highlightedOrders, setHighlightedOrders] = useState<HighlightedOrder>(
    {}
  );
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const soundIntervalRef = useRef<NodeJS.Timeout | null>(null);

  // Initialize filters with previous day as start, today as end
  const getTodayDateString = () => {
    const today = new Date();
    return today.toISOString().split("T")[0];
  };

  const getYesterdayDateString = () => {
    const yesterday = subDays(new Date(), 1);
    return yesterday.toISOString().split("T")[0];
  };

  const todayDateString = getTodayDateString();
  const yesterdayDateString = getYesterdayDateString();
  const [filters] = useState({
    startDate: yesterdayDateString,
    endDate: todayDateString,
  });

  console.log("[Admin] Date filter range:", {
    yesterdayDateString,
    todayDateString,
  });

  // Fetch all orders for previous day through today
  const fetchAllOrders = useCallback(async () => {
    try {
      const queryParams = new URLSearchParams();
      queryParams.append("startDate", yesterdayDateString);
      queryParams.append("endDate", todayDateString);

      const url = `/admin/orders?${queryParams.toString()}`;
      console.log("[Admin] Fetching orders from:", url);
      const data = await apiClient.get(url);
      console.log("[Admin] Fetched orders count:", data.orders?.length);
      setAllOrders(data.orders);
    } catch (error) {
      console.error("[Admin] Error fetching orders:", error);
      errorLogger.error(
        "Error fetching all orders for current day range",
        {},
        error instanceof Error ? error : new Error(String(error))
      );
    }
  }, [todayDateString, yesterdayDateString]);

  // Fetch filtered orders for previous day through today
  const fetchOrders = useCallback(async () => {
    try {
      setLoading(true);
      const queryParams = new URLSearchParams();
      queryParams.append("startDate", yesterdayDateString);
      queryParams.append("endDate", todayDateString);

      const url = `/admin/orders?${queryParams.toString()}`;
      const data = await apiClient.get(url);
      setOrders(data.orders);
    } catch (error) {
      errorLogger.error(
        "Error fetching admin orders for date range",
        {},
        error instanceof Error ? error : new Error(String(error))
      );
    } finally {
      setLoading(false);
    }
  }, [todayDateString, yesterdayDateString]);

  // Trigger fetch when component mounts
  useEffect(() => {
    fetchOrders();
  }, [fetchOrders]);

  // Smart append new order without refetch
  const handleNewOrder = useCallback(
    (newOrder: IOrder) => {
      console.log("[Admin] New order received via Socket.io:", newOrder);

      // Check if order is within our date range (yesterday through today)
      const orderDate = new Date(newOrder.createdAt || new Date());
      const orderDateString = orderDate.toISOString().split("T")[0];

      console.log(
        "[Admin] Order date:",
        orderDateString,
        "Filter range:",
        yesterdayDateString,
        "-",
        todayDateString
      );

      // Only add if within date range
      if (
        orderDateString >= yesterdayDateString &&
        orderDateString <= todayDateString
      ) {
        // Add to allOrders
        setAllOrders((prev) => {
          const newList = [newOrder, ...prev];
          console.log(
            "[Admin] Updated allOrders, total count:",
            newList.length
          );
          return newList;
        });

        // Add to orders list since we're always showing today's orders
        setOrders((prev) => {
          const newList = [newOrder, ...prev];
          console.log("[Admin] Updated orders, total count:", newList.length);
          return newList;
        });

        // Highlight new order for 2 seconds
        setHighlightedOrders((prev) => ({ ...prev, [newOrder._id]: true }));
        setTimeout(() => {
          setHighlightedOrders((prev) => {
            const updated = { ...prev };
            delete updated[newOrder._id];
            return updated;
          });
        }, 5000);

        // Play notification sound
        if (audioRef.current) {
          audioRef.current.play().catch(() => {
            // Silent fail if audio can't play
          });
        }
      } else {
        console.log("[Admin] Order outside date range, skipping");
      }
    },
    [yesterdayDateString, todayDateString]
  );

  // Smart update order without refetch
  const handleOrderUpdate = useCallback((updatedOrder: IOrder) => {
    console.log("[Admin] Order updated:", updatedOrder);

    setOrders((prev) =>
      prev.map((order) =>
        order._id === updatedOrder._id ? updatedOrder : order
      )
    );

    setAllOrders((prev) =>
      prev.map((order) =>
        order._id === updatedOrder._id ? updatedOrder : order
      )
    );
  }, []);

  // Smart status change
  const handleStatusChange = useCallback(
    (data: { orderId: string; status: string }) => {
      console.log("[Admin] Order status changed:", data);

      setOrders((prev) =>
        prev.map((order) =>
          order._id === data.orderId
            ? { ...order, status: data.status as any }
            : order
        )
      );

      setAllOrders((prev) =>
        prev.map((order) =>
          order._id === data.orderId
            ? { ...order, status: data.status as any }
            : order
        )
      );
    },
    []
  );

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

    // Check if user is admin
    if (session?.user?.role !== "admin") {
      router.push("/");
      return;
    }

    // Initial fetch
    fetchAllOrders();

    // Setup Socket.io listeners for real-time updates
    if (orderEvents.orderCreated) {
      console.log("[Admin] Setting up orderCreated Socket.io listener");
      orderEvents.orderCreated((data) => {
        console.log("[Admin] Socket.io orderCreated event received:", data);
        handleNewOrder(data);
      });
    }

    if (orderEvents.orderUpdated) {
      console.log("[Admin] Setting up orderUpdated Socket.io listener");
      orderEvents.orderUpdated(handleOrderUpdate);
    }

    if (orderEvents.orderStatusChanged) {
      console.log("[Admin] Setting up orderStatusChanged Socket.io listener");
      orderEvents.orderStatusChanged(handleStatusChange);
    }

    // Aggressive polling: fetch every 2 seconds regardless of connection status
    // This ensures we catch all orders
    const aggressivePollInterval = setInterval(() => {
      console.log("[Admin] Aggressive poll triggered (every 2s)");
      fetchAllOrders();
    }, 10000);

    return () => {
      clearInterval(aggressivePollInterval);
    };
  }, [
    status,
    session,
    router,
    fetchAllOrders,
    isConnected,
    orderEvents,
    handleNewOrder,
    handleOrderUpdate,
    handleStatusChange,
  ]);

  // Setup 1-minute sound interval for pending orders
  useEffect(() => {
    // Clear existing interval
    if (soundIntervalRef.current) {
      clearInterval(soundIntervalRef.current);
      soundIntervalRef.current = null;
    }

    // Get pending orders count
    const pendingCount = allOrders.filter(
      (order) => order.status === "pending"
    ).length;

    // Only set interval if there are pending orders
    if (pendingCount > 0) {
      console.log(
        `[Admin] ${pendingCount} pending orders - starting 1-minute sound interval`
      );

      // Play sound immediately
      if (audioRef.current) {
        audioRef.current.play().catch(() => {
          // Silent fail if audio can't play
        });
      }

      // Play sound every minute
      soundIntervalRef.current = setInterval(() => {
        if (audioRef.current) {
          audioRef.current.play().catch(() => {
            // Silent fail if audio can't play
          });
        }
      }, 60000); // 1 minute interval
    }

    return () => {
      if (soundIntervalRef.current) {
        clearInterval(soundIntervalRef.current);
        soundIntervalRef.current = null;
      }
    };
  }, [allOrders]);

  const handleAcceptOrder = async (orderId: string, prepTime: number) => {
    try {
      const data = await apiClient.put(`/admin/orders/${orderId}`, {
        status: "accepted",
        estimatedPrepTime: prepTime,
      });
      handleOrderUpdate(data.order);
      setSelectedOrder(null);
    } catch (error) {
      console.error("Error accepting order:", error);
    }
  };

  const handleRejectOrder = async (
    orderId: string,
    reason: string,
    comment?: string
  ) => {
    try {
      const data = await apiClient.put(`/admin/orders/${orderId}`, {
        status: "rejected",
        rejectionReason: reason,
        rejectionComment: comment,
      });
      handleOrderUpdate(data.order);
      setSelectedOrder(null);
    } catch (error) {
      console.error("Error rejecting order:", error);
    }
  };

  const handleCompleteOrder = async (orderId: string) => {
    try {
      const data = await apiClient.put(`/admin/orders/${orderId}`, {
        status: "completed",
      });
      handleOrderUpdate(data.order);
      setSelectedOrder(null);
    } catch (error) {
      console.error("Error completing order:", error);
    }
  };

  const getToppingName = (topping: any) => {
    return topping && typeof topping === "object"
      ? topping.name || ""
      : topping || "";
  };

  // Group orders by status
  const groupOrdersByStatus = (orders: IOrder[]) => ({
    pending: orders.filter((order) => order.status === "pending"),
    accepted: orders.filter((order) => order.status === "accepted"),
    completed: orders.filter((order) => order.status === "completed"),
    rejected: orders.filter((order) => order.status === "rejected"),
  });

  const {
    pending: pendingOrders,
    accepted: acceptedOrders,
    completed: completedOrders,
    rejected: rejectedOrders,
  } = groupOrdersByStatus(allOrders);

  // Show loading state while checking authentication
  if (status === "loading" || session?.user?.role !== "admin") {
    return (
      <div className="min-h-screen bg-gray-50 py-8 px-4">
        <div className="max-w-7xl mx-auto">
          <div className="mb-6 h-8 bg-gray-300 rounded w-48 animate-pulse"></div>
          <OrderGridSkeleton count={6} />
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 py-8 px-4 sm:px-6 lg:px-8">
      {/* Hidden audio element for notifications */}
      <audio ref={audioRef} src="/audio/drop-coin.mp3" />

      <div className="max-w-7xl mx-auto">
        {/* Header with Date */}
        <div className="mb-8">
          <div className="flex items-center justify-between">
            <div>
              <h1 className="text-4xl font-bold text-gray-900">
                {t.dashboard}
              </h1>
              <p className="text-gray-600 mt-2">
                {t.realTimeOrders} - {format(new Date(), "EEEE, MMMM d, yyyy")}
              </p>
            </div>
            <button
              onClick={() => {
                console.log("[Admin] Manual refresh clicked");
                fetchAllOrders();
              }}
              className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-semibold transition"
            >
              🔄 Refresh
            </button>
          </div>
        </div>

        {/* Stats Section */}
        <div className="grid grid-cols-1 md:grid-cols-5 gap-6 mb-8">
          <StatCard
            label={t.totalOrders}
            value={allOrders.length}
            color="gray"
          />
          <StatCard
            label={t.pending}
            value={pendingOrders.length}
            color="yellow"
          />
          <StatCard
            label={t.accepted}
            value={acceptedOrders.length}
            color="blue"
          />
          <StatCard
            label={t.completed}
            value={completedOrders.length}
            color="green"
          />
          <StatCard
            label={t.rejected}
            value={rejectedOrders.length}
            color="red"
          />
        </div>

        {/* Kanban Board with Right Sidebar */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
          {/* Left side - Kanban Board */}
          <div className="lg:col-span-2">
            <div className="flex items-center gap-4 mb-6">
              <h2 className="text-2xl font-bold text-gray-900">
                {t.orderBoard}
              </h2>
              <div className="flex gap-2 border-b border-gray-200">
                <button
                  onClick={() => setActiveTab("active")}
                  className={`px-6 py-3 font-semibold transition-colors ${
                    activeTab === "active"
                      ? "text-black border-b-2 border-[#ffd119]"
                      : "text-gray-600 hover:text-gray-900"
                  }`}
                >
                  {t.activeOrders}
                </button>
                <button
                  onClick={() => setActiveTab("completed")}
                  className={`px-6 py-3 font-semibold transition-colors ${
                    activeTab === "completed"
                      ? "text-gray-900 border-b-2 border-gray-900"
                      : "text-gray-600 hover:text-gray-900"
                  }`}
                >
                  {t.completedRejected}
                </button>
              </div>
            </div>

            {loading && (
              <div className="p-6">
                <OrderGridSkeleton count={5} />
              </div>
            )}

            {!loading && activeTab === "active" && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <OrderColumnSection
                  title={t.pending}
                  orders={pendingOrders}
                  selectedOrderId={selectedOrder?._id}
                  highlightedOrders={highlightedOrders}
                  onSelectOrder={setSelectedOrder}
                  statusColor="yellow"
                  headerColor="yellow"
                />
                <OrderColumnSection
                  title={t.accepted}
                  orders={acceptedOrders}
                  selectedOrderId={selectedOrder?._id}
                  highlightedOrders={highlightedOrders}
                  onSelectOrder={setSelectedOrder}
                  statusColor="blue"
                  headerColor="blue"
                />
              </div>
            )}

            {!loading && activeTab === "completed" && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <OrderColumnSection
                  title={t.completed}
                  orders={completedOrders}
                  selectedOrderId={selectedOrder?._id}
                  highlightedOrders={highlightedOrders}
                  onSelectOrder={setSelectedOrder}
                  statusColor="green"
                  headerColor="green"
                />
                <OrderColumnSection
                  title={t.rejected}
                  orders={rejectedOrders}
                  selectedOrderId={selectedOrder?._id}
                  highlightedOrders={highlightedOrders}
                  onSelectOrder={setSelectedOrder}
                  statusColor="red"
                  headerColor="red"
                />
              </div>
            )}
          </div>

          {/* Right side - Order Details Sidebar */}
          {selectedOrder && (
            <OrderDetailsSidebar
              order={selectedOrder}
              onAccept={async (prepTime) => {
                try {
                  const data = await apiClient.put(
                    `/admin/orders/${selectedOrder._id}`,
                    {
                      status: "accepted",
                      estimatedPrepTime: prepTime,
                    }
                  );
                  handleOrderUpdate(data.order);
                  setSelectedOrder(null);
                } catch (error) {
                  console.error("Error accepting order:", error);
                }
              }}
              onReject={async (reason, comment) => {
                try {
                  const data = await apiClient.put(
                    `/admin/orders/${selectedOrder._id}`,
                    {
                      status: "rejected",
                      rejectionReason: reason,
                      rejectionComment: comment,
                    }
                  );
                  handleOrderUpdate(data.order);
                  setSelectedOrder(null);
                } catch (error) {
                  console.error("Error rejecting order:", error);
                }
              }}
              onComplete={async () => {
                try {
                  const data = await apiClient.put(
                    `/admin/orders/${selectedOrder._id}`,
                    { status: "completed" }
                  );
                  handleOrderUpdate(data.order);
                  setSelectedOrder(null);
                } catch (error) {
                  console.error("Error completing order:", error);
                }
              }}
            />
          )}
        </div>
      </div>
    </div>
  );
}

const AdminDashboard = withErrorBoundary(
  AdminDashboardContent,
  "Admin Dashboard"
);
export default AdminDashboard;
