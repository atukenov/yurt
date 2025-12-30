"use client";

import { withErrorBoundary } from "@/components/ErrorBoundary";
import { OrderFilterPanel, OrderFilters } from "@/components/OrderFilterPanel";
import { OrderGridSkeleton } from "@/components/SkeletonLoaders";
import { useSocket } from "@/components/SocketProvider";
import { errorLogger } from "@/lib/logger";
import { exportOrdersToCSV, exportOrdersToPDF } from "@/lib/orderExport";
import { IOrder, IOrderItem } from "@/types";
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

  const getCustomerName = (customer: IOrder["customer"]) => {
    return customer && typeof customer === "object" ? customer.name : "";
  };

  const getCustomerEmail = (customer: IOrder["customer"]) => {
    return customer && typeof customer === "object" ? customer.email : "";
  };

  const getCustomerPhone = (customer: IOrder["customer"]) => {
    return customer && typeof customer === "object" ? customer.phone || "" : "";
  };

  const getLocationName = (location: IOrder["location"]) => {
    return location && typeof location === "object" ? location.name : "";
  };

  const getMenuItemName = (menuItem: IOrderItem["menuItem"]) => {
    return menuItem && typeof menuItem === "object" ? menuItem.name || "" : "";
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

        {/* Kanban Board Section with Tabs */}
        <div>
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
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
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
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
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

        {/* Order Details Panel - Below Kanban */}
        {selectedOrder && (
          <div className="mt-8 bg-white rounded-lg shadow p-6">
            <h2 className="text-2xl font-bold text-gray-900 mb-6">
              Order Details
            </h2>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="space-y-4">
                <div>
                  <p className="text-xs text-gray-600 uppercase">
                    Order Number
                  </p>
                  <p className="font-semibold text-gray-900 text-lg">
                    {selectedOrder.orderNumber}
                  </p>
                </div>

                <div>
                  <p className="text-xs text-gray-600 uppercase">Customer</p>
                  <p className="font-semibold text-gray-900">
                    {getCustomerName(selectedOrder.customer)}
                  </p>
                  <p className="text-sm text-gray-600">
                    {getCustomerEmail(selectedOrder.customer)}
                  </p>
                  <p className="text-sm text-gray-600">
                    {getCustomerPhone(selectedOrder.customer)}
                  </p>
                </div>

                <div>
                  <p className="text-xs text-gray-600 uppercase">Location</p>
                  <p className="font-semibold text-gray-900">
                    {getLocationName(selectedOrder.location)}
                  </p>
                </div>

                <div>
                  <p className="text-xs text-gray-600 uppercase mb-2">Items</p>
                  <div className="space-y-1 text-sm text-gray-700">
                    {selectedOrder.items?.map((item, idx) => (
                      <p key={idx}>
                        • {getMenuItemName(item.menuItem)} x{item.quantity} (
                        {item.size})
                      </p>
                    ))}
                  </div>
                </div>

                {selectedOrder.notes && (
                  <div>
                    <p className="text-xs text-gray-600 uppercase">Notes</p>
                    <p className="text-sm text-gray-700">
                      {selectedOrder.notes}
                    </p>
                  </div>
                )}
              </div>

              {/* Action Buttons */}
              <div className="bg-gray-50 rounded-lg p-6">
                <h3 className="text-lg font-bold text-gray-900 mb-4">
                  Actions
                </h3>

                {selectedOrder.status === "pending" && (
                  <div className="space-y-3">
                    <select
                      defaultValue="15"
                      id="prep-time"
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                    >
                      <option value="10">10 minutes</option>
                      <option value="15">15 minutes</option>
                      <option value="30">30 minutes</option>
                    </select>
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
                      Accept Order
                    </button>

                    <select
                      defaultValue="no_milk"
                      id="reject-reason"
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500"
                    >
                      <option value="no_milk">No Milk</option>
                      <option value="no_coffee_beans">No Coffee Beans</option>
                      <option value="size_unavailable">Size Unavailable</option>
                      <option value="equipment_issue">Equipment Issue</option>
                      <option value="custom">Custom Reason</option>
                    </select>

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
                        handleRejectOrder(selectedOrder._id, reason, comment);
                      }}
                      className="w-full px-4 py-2 bg-red-600 text-white rounded-lg hover:bg-red-700 font-semibold transition"
                    >
                      Reject Order
                    </button>
                  </div>
                )}

                {selectedOrder.status === "accepted" && (
                  <button
                    onClick={() => handleCompleteOrder(selectedOrder._id)}
                    className="w-full px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 font-semibold transition"
                  >
                    Mark as Completed
                  </button>
                )}

                {selectedOrder.status === "completed" && (
                  <div className="bg-green-100 border border-green-300 rounded-lg p-4 text-center">
                    <p className="text-green-800 font-semibold">
                      Order Completed ✓
                    </p>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

const AdminDashboard = withErrorBoundary(
  AdminDashboardContent,
  "Admin Dashboard"
);
export default AdminDashboard;
