"use client";

import { ExportSection } from "@/components/admin/ExportSection";
import { OrderColumnSection } from "@/components/admin/OrderColumnSection";
import { OrderDetailsSidebar } from "@/components/admin/OrderDetailsSidebar";
import { StatCard } from "@/components/admin/StatCard";
import { withErrorBoundary } from "@/components/ErrorBoundary";
import { OrderFilterPanel, OrderFilters } from "@/components/OrderFilterPanel";
import { OrderGridSkeleton } from "@/components/SkeletonLoaders";
import { useSocket } from "@/components/SocketProvider";
import { errorLogger } from "@/lib/logger";
import { IOrder } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";

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

  const [orders, setOrders] = useState<IOrder[]>([]);
  const [allOrders, setAllOrders] = useState<IOrder[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [selectedOrder, setSelectedOrder] = useState<IOrder | null>(null);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<"active" | "completed">("active");
  const [highlightedOrders, setHighlightedOrders] = useState<HighlightedOrder>(
    {}
  );
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const soundIntervalRef = useRef<NodeJS.Timeout | null>(null);

  // Initialize filters with today's date
  const getTodayDateString = () => {
    const today = new Date();
    return today.toISOString().split("T")[0];
  };

  const [filters, setFilters] = useState<OrderFilters>({
    startDate: getTodayDateString(),
    endDate: getTodayDateString(),
  });

  // Fetch locations for filter dropdown
  useEffect(() => {
    const fetchLocations = async () => {
      try {
        const res = await fetch("/api/locations");
        if (res.ok) {
          const data = await res.json();
          setLocations(data.locations || []);
        }
      } catch (error) {
        errorLogger.error(
          "Error fetching locations",
          {},
          error instanceof Error ? error : new Error(String(error))
        );
      }
    };
    fetchLocations();
  }, []);

  // Fetch all orders for status overview cards (initial load only)
  const fetchAllOrders = useCallback(async () => {
    try {
      const res = await fetch("/api/admin/orders");
      if (res.ok) {
        const data = await res.json();
        setAllOrders(data.orders);
      }
    } catch (error) {
      errorLogger.error(
        "Error fetching all orders for overview",
        {},
        error instanceof Error ? error : new Error(String(error))
      );
    }
  }, []);

  // Fetch filtered orders
  const fetchOrders = useCallback(async () => {
    try {
      setLoading(true);
      const queryParams = new URLSearchParams();

      if (filters.status) queryParams.append("status", filters.status);
      if (filters.paymentMethod)
        queryParams.append("paymentMethod", filters.paymentMethod);
      if (filters.locationId)
        queryParams.append("locationId", filters.locationId);
      if (filters.searchQuery)
        queryParams.append("searchQuery", filters.searchQuery);
      if (filters.startDate) queryParams.append("startDate", filters.startDate);
      if (filters.endDate) queryParams.append("endDate", filters.endDate);

      const url = `/api/admin/orders?${queryParams.toString()}`;
      const res = await fetch(url);
      if (res.ok) {
        const data = await res.json();
        setOrders(data.orders);
      } else {
        errorLogger.warn("Failed to fetch admin orders", { filters });
      }
    } catch (error) {
      errorLogger.error(
        "Error fetching admin orders",
        { filters },
        error instanceof Error ? error : new Error(String(error))
      );
    } finally {
      setLoading(false);
    }
  }, [filters]);

  // Smart append new order without refetch
  const handleNewOrder = useCallback(
    (newOrder: IOrder) => {
      console.log("[Admin] New order received:", newOrder);

      // Add to allOrders
      setAllOrders((prev) => [newOrder, ...prev]);

      // Only add to orders list if it matches current filters
      const matchesFilters =
        (!filters.locationId ||
          (newOrder.location &&
            (typeof newOrder.location === "string"
              ? newOrder.location === filters.locationId
              : (newOrder.location as any)._id === filters.locationId))) &&
        (!filters.paymentMethod ||
          newOrder.paymentMethod === filters.paymentMethod);

      if (matchesFilters) {
        setOrders((prev) => [newOrder, ...prev]);
        // Highlight new order for 2 seconds
        setHighlightedOrders((prev) => ({ ...prev, [newOrder._id]: true }));
        setTimeout(() => {
          setHighlightedOrders((prev) => {
            const updated = { ...prev };
            delete updated[newOrder._id];
            return updated;
          });
        }, 2000);

        // Play notification sound
        if (audioRef.current) {
          audioRef.current.play().catch(() => {
            // Silent fail if audio can't play
          });
        }
      }
    },
    [filters]
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
    fetchOrders();
    fetchAllOrders();

    // Setup Socket.io listeners for real-time updates
    if (orderEvents.orderCreated) {
      orderEvents.orderCreated(handleNewOrder);
    }

    if (orderEvents.orderUpdated) {
      orderEvents.orderUpdated(handleOrderUpdate);
    }

    if (orderEvents.orderStatusChanged) {
      orderEvents.orderStatusChanged(handleStatusChange);
    }

    // Fallback to silent polling every 10 seconds if Socket.io is not connected
    const pollInterval = setInterval(() => {
      if (!isConnected) {
        console.log("[Admin] WebSocket not connected, using silent polling...");
        fetchOrders();
        fetchAllOrders();
      }
    }, 10000);

    return () => {
      clearInterval(pollInterval);
    };
  }, [
    status,
    session,
    filters,
    router,
    fetchOrders,
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
      const res = await fetch(`/api/admin/orders/${orderId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          status: "accepted",
          estimatedPrepTime: prepTime,
        }),
      });

      if (res.ok) {
        // Update state optimistically
        const updatedOrder = await res.json();
        handleOrderUpdate(updatedOrder.order);
        setSelectedOrder(null);
      }
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
      const res = await fetch(`/api/admin/orders/${orderId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          status: "rejected",
          rejectionReason: reason,
          rejectionComment: comment,
        }),
      });

      if (res.ok) {
        const updatedOrder = await res.json();
        handleOrderUpdate(updatedOrder.order);
        setSelectedOrder(null);
      }
    } catch (error) {
      console.error("Error rejecting order:", error);
    }
  };

  const handleCompleteOrder = async (orderId: string) => {
    try {
      const res = await fetch(`/api/admin/orders/${orderId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status: "completed" }),
      });

      if (res.ok) {
        const updatedOrder = await res.json();
        handleOrderUpdate(updatedOrder.order);
        setSelectedOrder(null);
      }
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
        {/* Header */}
        <div className="mb-8">
          <div>
            <h1 className="text-4xl font-bold text-gray-900">
              Admin Dashboard
            </h1>
            <p className="text-gray-600 mt-2">
              Real-time order management with instant updates
            </p>
          </div>
        </div>

        {/* Filter Panel */}
        <div className="mt-6 mb-8">
          <OrderFilterPanel
            onFiltersChange={setFilters}
            locations={locations}
          />
        </div>

        {/* Stats Section */}
        <div className="grid grid-cols-1 md:grid-cols-5 gap-6 mb-8">
          <StatCard
            label="Total Orders"
            value={allOrders.length}
            color="gray"
          />
          <StatCard
            label="Pending"
            value={pendingOrders.length}
            color="yellow"
          />
          <StatCard
            label="Accepted"
            value={acceptedOrders.length}
            color="blue"
          />
          <StatCard
            label="Completed"
            value={completedOrders.length}
            color="green"
          />
          <StatCard
            label="Rejected"
            value={rejectedOrders.length}
            color="red"
          />
        </div>

        {/* Export Section */}
        <ExportSection orders={allOrders} />

        {/* Kanban Board with Right Sidebar */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
          {/* Left side - Kanban Board */}
          <div className="lg:col-span-2">
            <div className="flex items-center gap-4 mb-6">
              <h2 className="text-2xl font-bold text-gray-900">Order Board</h2>
              <div className="flex gap-2 border-b border-gray-200">
                <button
                  onClick={() => setActiveTab("active")}
                  className={`px-6 py-3 font-semibold transition-colors ${
                    activeTab === "active"
                      ? "text-amber-600 border-b-2 border-amber-600"
                      : "text-gray-600 hover:text-gray-900"
                  }`}
                >
                  Active Orders
                </button>
                <button
                  onClick={() => setActiveTab("completed")}
                  className={`px-6 py-3 font-semibold transition-colors ${
                    activeTab === "completed"
                      ? "text-gray-900 border-b-2 border-gray-900"
                      : "text-gray-600 hover:text-gray-900"
                  }`}
                >
                  Completed & Rejected
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
                  title="Pending"
                  orders={pendingOrders}
                  selectedOrderId={selectedOrder?._id}
                  highlightedOrders={highlightedOrders}
                  onSelectOrder={setSelectedOrder}
                  statusColor="yellow"
                  headerColor="yellow"
                />
                <OrderColumnSection
                  title="Accepted"
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
                  title="Completed"
                  orders={completedOrders}
                  selectedOrderId={selectedOrder?._id}
                  highlightedOrders={highlightedOrders}
                  onSelectOrder={setSelectedOrder}
                  statusColor="green"
                  headerColor="green"
                />
                <OrderColumnSection
                  title="Rejected"
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
                  const res = await fetch(
                    `/api/admin/orders/${selectedOrder._id}`,
                    {
                      method: "PUT",
                      headers: { "Content-Type": "application/json" },
                      body: JSON.stringify({
                        status: "accepted",
                        estimatedPrepTime: prepTime,
                      }),
                    }
                  );

                  if (res.ok) {
                    const updatedOrder = await res.json();
                    handleOrderUpdate(updatedOrder.order);
                    setSelectedOrder(null);
                  }
                } catch (error) {
                  console.error("Error accepting order:", error);
                }
              }}
              onReject={async (reason, comment) => {
                try {
                  const res = await fetch(
                    `/api/admin/orders/${selectedOrder._id}`,
                    {
                      method: "PUT",
                      headers: { "Content-Type": "application/json" },
                      body: JSON.stringify({
                        status: "rejected",
                        rejectionReason: reason,
                        rejectionComment: comment,
                      }),
                    }
                  );

                  if (res.ok) {
                    const updatedOrder = await res.json();
                    handleOrderUpdate(updatedOrder.order);
                    setSelectedOrder(null);
                  }
                } catch (error) {
                  console.error("Error rejecting order:", error);
                }
              }}
              onComplete={async () => {
                try {
                  const res = await fetch(
                    `/api/admin/orders/${selectedOrder._id}`,
                    {
                      method: "PUT",
                      headers: { "Content-Type": "application/json" },
                      body: JSON.stringify({ status: "completed" }),
                    }
                  );

                  if (res.ok) {
                    const updatedOrder = await res.json();
                    handleOrderUpdate(updatedOrder.order);
                    setSelectedOrder(null);
                  }
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
