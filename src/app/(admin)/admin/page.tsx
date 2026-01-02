"use client";

import { withErrorBoundary } from "@/components/ErrorBoundary";
import { OrderFilterPanel, OrderFilters } from "@/components/OrderFilterPanel";
import { OrderGridSkeleton } from "@/components/SkeletonLoaders";
import { useSocket } from "@/components/SocketProvider";
import {
  getCustomerEmail,
  getCustomerName,
  getCustomerPhone,
  getLocationName,
  getMenuItemName,
} from "@/lib/helpers";
import { errorLogger } from "@/lib/logger";
import { exportOrdersToCSV, exportOrdersToPDF } from "@/lib/orderExport";
import { IOrder } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

interface Location {
  _id: string;
  name: string;
}

function AdminDashboardContent() {
  const router = useRouter();
  const { data: session, status } = useSession();
  const { isConnected, isAvailable, orderEvents } = useSocket();

  const [orders, setOrders] = useState<IOrder[]>([]);
  const [allOrders, setAllOrders] = useState<IOrder[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [selectedOrder, setSelectedOrder] = useState<IOrder | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);
  const [activeTab, setActiveTab] = useState<"active" | "completed">("active");

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

  // Fetch all orders for status overview cards
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

  const fetchOrders = useCallback(async () => {
    try {
      setLoading(true);
      // Build query string from filters
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

    fetchOrders();
    fetchAllOrders();

    // Setup Socket.io listeners for real-time updates
    if (orderEvents.orderCreated) {
      orderEvents.orderCreated((newOrder: IOrder) => {
        console.log("[Admin] New order received:", newOrder);
        setOrders((prev) => [newOrder, ...prev]);
        // Also update allOrders for live count updates
        setAllOrders((prev) => [newOrder, ...prev]);
      });
    }

    if (orderEvents.orderUpdated) {
      orderEvents.orderUpdated((updatedOrder: any) => {
        console.log("[Admin] Order updated:", updatedOrder);
        setOrders((prev) =>
          prev.map((order) =>
            order._id === updatedOrder._id ? updatedOrder : order
          )
        );
        // Also update allOrders
        setAllOrders((prev) =>
          prev.map((order) =>
            order._id === updatedOrder._id ? updatedOrder : order
          )
        );
      });
    }

    if (orderEvents.orderStatusChanged) {
      orderEvents.orderStatusChanged(
        (data: { orderId: string; status: string }) => {
          console.log("[Admin] Order status changed:", data);
          setOrders((prev) =>
            prev.map((order) =>
              order._id === data.orderId
                ? { ...order, status: data.status as any }
                : order
            )
          );
          // Also update allOrders for live count updates
          setAllOrders((prev) =>
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
        console.log("[Admin] WebSocket not connected, using polling...");
        fetchOrders();
      }
    }, 10000);

    return () => clearInterval(pollInterval);
  }, [status, session, filters, router, fetchOrders, fetchAllOrders]);

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
        fetchOrders();
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
        fetchOrders();
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
        fetchOrders();
        setSelectedOrder(null);
      }
    } catch (error) {
      console.error("Error completing order:", error);
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case "pending":
        return "bg-yellow-100 text-yellow-800";
      case "accepted":
        return "bg-blue-100 text-blue-800";
      case "completed":
        return "bg-green-100 text-green-800";
      case "rejected":
        return "bg-red-100 text-red-800";
      default:
        return "bg-gray-100 text-gray-800";
    }
  };

  const getToppingName = (topping: any) => {
    // Using imported getToppingName from helpers is preferred,
    // but keeping this wrapper for compatibility with local state
    return topping && typeof topping === "object"
      ? topping.name || ""
      : topping || "";
  };

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

  // Group orders by status for Kanban view
  const pendingOrders = allOrders.filter((order) => order.status === "pending");
  const acceptedOrders = allOrders.filter(
    (order) => order.status === "accepted"
  );
  const completedOrders = allOrders.filter(
    (order) => order.status === "completed"
  );
  const rejectedOrders = allOrders.filter(
    (order) => order.status === "rejected"
  );

  return (
    <div className="min-h-screen bg-gray-50 py-8 px-4 sm:px-6 lg:px-8">
      <div className="max-w-7xl mx-auto">
        <div className="mb-8">
          <h1 className="text-4xl font-bold text-gray-900">Admin Dashboard</h1>
          <p className="text-gray-600 mt-2">
            Manage orders in real-time with Kanban view
          </p>

          {/* Filter Panel */}
          <div className="mt-6">
            <OrderFilterPanel
              onFiltersChange={setFilters}
              locations={locations}
            />
          </div>
        </div>

        {/* Stats Section */}
        <div className="grid grid-cols-1 md:grid-cols-5 gap-6 mb-8">
          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600">Total Orders</p>
                <p className="text-3xl font-bold text-gray-900">
                  {allOrders.length}
                </p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600">Pending</p>
                <p className="text-3xl font-bold text-yellow-600">
                  {pendingOrders.length}
                </p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600">Accepted</p>
                <p className="text-3xl font-bold text-blue-600">
                  {acceptedOrders.length}
                </p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600">Completed</p>
                <p className="text-3xl font-bold text-green-600">
                  {completedOrders.length}
                </p>
              </div>
            </div>
          </div>
          <div className="bg-white rounded-lg shadow p-6">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-gray-600">Rejected</p>
                <p className="text-3xl font-bold text-red-600">
                  {rejectedOrders.length}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Export Buttons */}
        {allOrders.length > 0 && (
          <div className="flex gap-3 mb-8">
            <button
              onClick={async () => {
                try {
                  setExporting(true);
                  exportOrdersToCSV(
                    allOrders,
                    `orders-${new Date().toISOString().split("T")[0]}.csv`
                  );
                  errorLogger.info("Exported orders to CSV", {
                    count: allOrders.length,
                  });
                } catch (error) {
                  errorLogger.error(
                    "Error exporting CSV",
                    {},
                    error instanceof Error ? error : new Error("Export failed")
                  );
                  alert("Failed to export CSV");
                } finally {
                  setExporting(false);
                }
              }}
              disabled={exporting}
              className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 font-semibold transition flex items-center gap-2"
            >
              📥 Export CSV ({allOrders.length})
            </button>
            <button
              onClick={async () => {
                try {
                  setExporting(true);
                  exportOrdersToPDF(allOrders);
                  errorLogger.info("Exported orders to PDF", {
                    count: allOrders.length,
                  });
                } catch (error) {
                  errorLogger.error(
                    "Error exporting PDF",
                    {},
                    error instanceof Error ? error : new Error("Export failed")
                  );
                  alert("Failed to export PDF");
                } finally {
                  setExporting(false);
                }
              }}
              disabled={exporting}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 font-semibold transition flex items-center gap-2"
            >
              📄 Export PDF ({allOrders.length})
            </button>
          </div>
        )}

        {/* Kanban Board with Right Sidebar Layout */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
          {/* Left side - Kanban Board (2 columns on desktop) */}
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
                {/* Pending Column */}
                <div className="bg-white rounded-lg shadow">
                  <div className="border-b border-gray-200 px-6 py-4 bg-yellow-50">
                    <h3 className="text-lg font-bold text-gray-900">
                      Pending ({pendingOrders.length})
                    </h3>
                  </div>
                  <div className="p-4 space-y-4 max-h-96 overflow-y-auto">
                    {pendingOrders.length === 0 && (
                      <p className="text-center text-gray-500 py-8">
                        No pending orders
                      </p>
                    )}
                    {pendingOrders.map((order: IOrder) => (
                      <button
                        key={order._id}
                        onClick={() => setSelectedOrder(order)}
                        className={`w-full text-left border border-yellow-200 rounded-lg p-4 hover:shadow-lg transition-all cursor-pointer ${
                          selectedOrder?._id === order._id
                            ? "bg-yellow-100 border-yellow-500"
                            : "bg-yellow-50 hover:bg-yellow-100"
                        }`}
                      >
                        <div className="flex justify-between items-start mb-2">
                          <span className="font-bold text-gray-900">
                            {order.orderNumber}
                          </span>
                          <span className="text-xs bg-yellow-200 text-yellow-800 px-2 py-1 rounded">
                            Pending
                          </span>
                        </div>
                        <p className="text-sm text-gray-600 font-medium">
                          {getCustomerName(order.customer)}
                        </p>
                        <p className="text-sm text-gray-500">
                          {order.items?.length || 0} items
                        </p>
                        <p className="text-lg font-bold text-gray-900 mt-3">
                          ${order.totalPrice.toFixed(2)}
                        </p>
                        <p className="text-xs text-gray-500 mt-2">
                          {new Date(order.createdAt || "").toLocaleDateString()}
                        </p>
                      </button>
                    ))}
                  </div>
                </div>

                {/* Accepted Column */}
                <div className="bg-white rounded-lg shadow">
                  <div className="border-b border-gray-200 px-6 py-4 bg-blue-50">
                    <h3 className="text-lg font-bold text-gray-900">
                      Accepted ({acceptedOrders.length})
                    </h3>
                  </div>
                  <div className="p-4 space-y-4 max-h-96 overflow-y-auto">
                    {acceptedOrders.length === 0 && (
                      <p className="text-center text-gray-500 py-8">
                        No accepted orders
                      </p>
                    )}
                    {acceptedOrders.map((order: IOrder) => (
                      <button
                        key={order._id}
                        onClick={() => setSelectedOrder(order)}
                        className={`w-full text-left border border-blue-200 rounded-lg p-4 hover:shadow-lg transition-all cursor-pointer ${
                          selectedOrder?._id === order._id
                            ? "bg-blue-100 border-blue-500"
                            : "bg-blue-50 hover:bg-blue-100"
                        }`}
                      >
                        <div className="flex justify-between items-start mb-2">
                          <span className="font-bold text-gray-900">
                            {order.orderNumber}
                          </span>
                          <span className="text-xs bg-blue-200 text-blue-800 px-2 py-1 rounded">
                            Accepted
                          </span>
                        </div>
                        <p className="text-sm text-gray-600 font-medium">
                          {getCustomerName(order.customer)}
                        </p>
                        <p className="text-sm text-gray-500">
                          {order.items?.length || 0} items
                        </p>
                        <p className="text-lg font-bold text-gray-900 mt-3">
                          ${order.totalPrice.toFixed(2)}
                        </p>
                        <p className="text-xs text-gray-500 mt-2">
                          {new Date(order.createdAt || "").toLocaleDateString()}
                        </p>
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            )}

            {!loading && activeTab === "completed" && (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                {/* Completed Column */}
                <div className="bg-white rounded-lg shadow">
                  <div className="border-b border-gray-200 px-6 py-4 bg-green-50">
                    <h3 className="text-lg font-bold text-gray-900">
                      Completed ({completedOrders.length})
                    </h3>
                  </div>
                  <div className="p-4 space-y-4 max-h-96 overflow-y-auto">
                    {completedOrders.length === 0 && (
                      <p className="text-center text-gray-500 py-8">
                        No completed orders
                      </p>
                    )}
                    {completedOrders.map((order: IOrder) => (
                      <button
                        key={order._id}
                        onClick={() => setSelectedOrder(order)}
                        className={`w-full text-left border border-green-200 rounded-lg p-4 hover:shadow-lg transition-all cursor-pointer ${
                          selectedOrder?._id === order._id
                            ? "bg-green-100 border-green-500"
                            : "bg-green-50 hover:bg-green-100"
                        }`}
                      >
                        <div className="flex justify-between items-start mb-2">
                          <span className="font-bold text-gray-900">
                            {order.orderNumber}
                          </span>
                          <span className="text-xs bg-green-200 text-green-800 px-2 py-1 rounded">
                            Completed
                          </span>
                        </div>
                        <p className="text-sm text-gray-600 font-medium">
                          {getCustomerName(order.customer)}
                        </p>
                        <p className="text-sm text-gray-500">
                          {order.items?.length || 0} items
                        </p>
                        <p className="text-lg font-bold text-gray-900 mt-3">
                          ${order.totalPrice.toFixed(2)}
                        </p>
                        <p className="text-xs text-gray-500 mt-2">
                          {new Date(order.createdAt || "").toLocaleDateString()}
                        </p>
                      </button>
                    ))}
                  </div>
                </div>

                {/* Rejected Column */}
                <div className="bg-white rounded-lg shadow">
                  <div className="border-b border-gray-200 px-6 py-4 bg-red-50">
                    <h3 className="text-lg font-bold text-gray-900">
                      Rejected ({rejectedOrders.length})
                    </h3>
                  </div>
                  <div className="p-4 space-y-4 max-h-96 overflow-y-auto">
                    {rejectedOrders.length === 0 && (
                      <p className="text-center text-gray-500 py-8">
                        No rejected orders
                      </p>
                    )}
                    {rejectedOrders.map((order: IOrder) => (
                      <button
                        key={order._id}
                        onClick={() => setSelectedOrder(order)}
                        className={`w-full text-left border border-red-200 rounded-lg p-4 hover:shadow-lg transition-all cursor-pointer ${
                          selectedOrder?._id === order._id
                            ? "bg-red-100 border-red-500"
                            : "bg-red-50 hover:bg-red-100"
                        }`}
                      >
                        <div className="flex justify-between items-start mb-2">
                          <span className="font-bold text-gray-900">
                            {order.orderNumber}
                          </span>
                          <span className="text-xs bg-red-200 text-red-800 px-2 py-1 rounded">
                            Rejected
                          </span>
                        </div>
                        <p className="text-sm text-gray-600 font-medium">
                          {getCustomerName(order.customer)}
                        </p>
                        <p className="text-sm text-gray-500">
                          {order.items?.length || 0} items
                        </p>
                        <p className="text-lg font-bold text-gray-900 mt-3">
                          ${order.totalPrice.toFixed(2)}
                        </p>
                        <p className="text-xs text-gray-500 mt-2">
                          {new Date(order.createdAt || "").toLocaleDateString()}
                        </p>
                      </button>
                    ))}
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Right side - Order Details Sidebar */}
          {selectedOrder && (
            <div className="lg:col-span-1">
              <div className="bg-white rounded-lg shadow sticky top-8 max-h-[calc(100vh-10rem)] overflow-y-auto">
                <div className="border-b border-gray-200 px-6 py-4 bg-gradient-to-r from-amber-50 to-amber-100">
                  <h3 className="text-lg font-bold text-gray-900">
                    Order {selectedOrder.orderNumber}
                  </h3>
                  <p className="text-sm text-gray-600 mt-1">
                    {new Date(selectedOrder.createdAt || "").toLocaleString()}
                  </p>
                </div>

                <div className="p-6 space-y-6">
                  {/* Customer Info */}
                  <div>
                    <p className="text-xs text-gray-600 uppercase font-bold">
                      Customer
                    </p>
                    <p className="font-semibold text-gray-900 mt-2">
                      {getCustomerName(selectedOrder.customer)}
                    </p>
                    <p className="text-sm text-gray-600">
                      {getCustomerEmail(selectedOrder.customer)}
                    </p>
                    <p className="text-sm text-gray-600">
                      {getCustomerPhone(selectedOrder.customer)}
                    </p>
                  </div>

                  {/* Location Info */}
                  <div>
                    <p className="text-xs text-gray-600 uppercase font-bold">
                      Location
                    </p>
                    <p className="font-semibold text-gray-900 mt-2">
                      {getLocationName(selectedOrder.location)}
                    </p>
                  </div>

                  {/* Order Items with Detailed Info */}
                  <div>
                    <p className="text-xs text-gray-600 uppercase font-bold mb-3">
                      Items ({selectedOrder.items?.length || 0})
                    </p>
                    <div className="space-y-3">
                      {selectedOrder.items?.map((item, idx) => (
                        <div
                          key={idx}
                          className="bg-gray-50 rounded-lg p-3 border border-gray-200"
                        >
                          <div className="flex justify-between items-start mb-2">
                            <p className="font-semibold text-gray-900">
                              {getMenuItemName(item.menuItem)}
                            </p>
                            <span className="text-xs bg-amber-100 text-amber-800 px-2 py-1 rounded">
                              x{item.quantity}
                            </span>
                          </div>

                          <div className="text-sm text-gray-600 space-y-1">
                            <p>
                              <span className="font-medium">Size:</span>{" "}
                              {item.size.charAt(0).toUpperCase() +
                                item.size.slice(1)}
                            </p>

                            {item.toppings && item.toppings.length > 0 && (
                              <div>
                                <p className="font-medium">Toppings:</p>
                                <ul className="list-disc list-inside ml-0 text-xs">
                                  {item.toppings.map((topping, tIdx) => (
                                    <li key={tIdx}>
                                      {getToppingName(topping)}
                                    </li>
                                  ))}
                                </ul>
                              </div>
                            )}

                            {item.specialInstructions && (
                              <p>
                                <span className="font-medium">Note:</span>{" "}
                                {item.specialInstructions}
                              </p>
                            )}
                          </div>

                          <p className="text-sm font-semibold text-gray-900 mt-2">
                            ${item.priceAtOrder?.toFixed(2) || "0.00"}
                          </p>
                        </div>
                      ))}
                    </div>
                  </div>

                  {/* Order Notes */}
                  {selectedOrder.notes && (
                    <div>
                      <p className="text-xs text-gray-600 uppercase font-bold">
                        Notes
                      </p>
                      <p className="text-sm text-gray-700 mt-2 bg-blue-50 p-3 rounded border border-blue-200">
                        {selectedOrder.notes}
                      </p>
                    </div>
                  )}

                  {/* Rejection Reason */}
                  {selectedOrder.rejectionReason && (
                    <div>
                      <p className="text-xs text-gray-600 uppercase font-bold">
                        Rejection Reason
                      </p>
                      <p className="text-sm text-gray-700 mt-2 bg-red-50 p-3 rounded border border-red-200">
                        <span className="font-semibold">
                          {selectedOrder.rejectionReason}
                        </span>
                        {selectedOrder.rejectionComment && (
                          <>
                            <br />
                            {selectedOrder.rejectionComment}
                          </>
                        )}
                      </p>
                    </div>
                  )}

                  {/* Order Summary */}
                  <div className="border-t border-gray-200 pt-4">
                    <div className="flex justify-between items-center mb-3">
                      <span className="text-gray-600">Subtotal:</span>
                      <span className="font-semibold">
                        ${(selectedOrder.totalPrice || 0).toFixed(2)}
                      </span>
                    </div>
                    <div className="flex justify-between items-center mb-3">
                      <span className="text-gray-600">Payment:</span>
                      <span className="text-sm">
                        {selectedOrder.paymentMethod.charAt(0).toUpperCase() +
                          selectedOrder.paymentMethod.slice(1)}
                        {" • "}
                        <span
                          className={`font-semibold ${
                            selectedOrder.paymentStatus === "completed"
                              ? "text-green-600"
                              : "text-yellow-600"
                          }`}
                        >
                          {selectedOrder.paymentStatus.charAt(0).toUpperCase() +
                            selectedOrder.paymentStatus.slice(1)}
                        </span>
                      </span>
                    </div>
                    <div className="flex justify-between items-center mb-3">
                      <span className="text-gray-600">Status:</span>
                      <span
                        className={`text-sm font-semibold px-2 py-1 rounded ${
                          selectedOrder.status === "pending"
                            ? "bg-yellow-100 text-yellow-800"
                            : selectedOrder.status === "accepted"
                            ? "bg-blue-100 text-blue-800"
                            : selectedOrder.status === "completed"
                            ? "bg-green-100 text-green-800"
                            : "bg-red-100 text-red-800"
                        }`}
                      >
                        {selectedOrder.status.charAt(0).toUpperCase() +
                          selectedOrder.status.slice(1)}
                      </span>
                    </div>
                  </div>

                  {/* Action Buttons */}
                  <div className="bg-gray-50 rounded-lg p-4 space-y-3 border border-gray-200">
                    {selectedOrder.status === "pending" && (
                      <>
                        <div>
                          <label className="text-xs text-gray-600 uppercase font-bold">
                            Prep Time
                          </label>
                          <select
                            defaultValue="15"
                            id="prep-time"
                            className="w-full mt-1 px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                          >
                            <option value="10">10 minutes</option>
                            <option value="15">15 minutes</option>
                            <option value="30">30 minutes</option>
                          </select>
                        </div>
                        <button
                          onClick={() => {
                            const prepTime = parseInt(
                              (
                                document.getElementById(
                                  "prep-time"
                                ) as HTMLSelectElement
                              )?.value || "15"
                            );
                            handleAcceptOrder(selectedOrder._id, prepTime);
                          }}
                          className="w-full px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 font-semibold transition"
                        >
                          ✓ Accept Order
                        </button>

                        <div>
                          <label className="text-xs text-gray-600 uppercase font-bold">
                            Reject Reason
                          </label>
                          <select
                            defaultValue="no_milk"
                            id="reject-reason"
                            className="w-full mt-1 px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                          >
                            <option value="no_milk">No Milk</option>
                            <option value="no_coffee_beans">
                              No Coffee Beans
                            </option>
                            <option value="size_unavailable">
                              Size Unavailable
                            </option>
                            <option value="equipment_issue">
                              Equipment Issue
                            </option>
                            <option value="custom">Custom Reason</option>
                          </select>
                        </div>

                        <textarea
                          id="reject-comment"
                          placeholder="Additional comment (optional)"
                          className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500 text-sm"
                          rows={2}
                        />

                        <button
                          onClick={() => {
                            const reason =
                              (
                                document.getElementById(
                                  "reject-reason"
                                ) as HTMLSelectElement
                              )?.value || "";
                            const comment =
                              (
                                document.getElementById(
                                  "reject-comment"
                                ) as HTMLTextAreaElement
                              )?.value || "";
                            handleRejectOrder(
                              selectedOrder._id,
                              reason,
                              comment
                            );
                          }}
                          className="w-full px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 font-semibold transition"
                        >
                          ✗ Reject Order
                        </button>
                      </>
                    )}

                    {selectedOrder.status === "accepted" && (
                      <button
                        onClick={() => handleCompleteOrder(selectedOrder._id)}
                        className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-semibold transition"
                      >
                        ✓ Mark as Completed
                      </button>
                    )}

                    {selectedOrder.status === "completed" && (
                      <div className="bg-green-100 border border-green-300 rounded-lg p-3 text-center">
                        <p className="text-green-800 font-semibold">
                          ✓ Order Completed
                        </p>
                      </div>
                    )}

                    {selectedOrder.status === "rejected" && (
                      <div className="bg-red-100 border border-red-300 rounded-lg p-3 text-center">
                        <p className="text-red-800 font-semibold">
                          ✗ Order Rejected
                        </p>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
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
