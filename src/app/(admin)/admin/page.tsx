"use client";

import { OrderGridSkeleton } from "@/components/SkeletonLoaders";
import { useSocket } from "@/components/SocketProvider";
import { IOrder, IOrderItem } from "@/types";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

export default function AdminDashboard() {
  const router = useRouter();
  const { data: session, status } = useSession();
  const { isConnected, isAvailable, orderEvents } = useSocket();

  const [orders, setOrders] = useState<IOrder[]>([]);
  const [selectedOrder, setSelectedOrder] = useState<IOrder | null>(null);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<
    "pending" | "accepted" | "rejected" | "completed" | "all"
  >("pending");

  const fetchOrders = useCallback(async () => {
    try {
      const url =
        filter === "all"
          ? "/api/admin/orders"
          : `/api/admin/orders?status=${filter}`;
      const res = await fetch(url);
      if (res.ok) {
        const data = await res.json();
        setOrders(data.orders);
      }
    } catch (error) {
      console.error("Error fetching orders:", error);
    } finally {
      setLoading(false);
    }
  }, [filter]);

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

    // Setup Socket.io listeners for real-time updates
    if (orderEvents.orderCreated) {
      orderEvents.orderCreated((newOrder: IOrder) => {
        console.log("[Admin] New order received:", newOrder);
        setOrders((prev) => [newOrder, ...prev]);
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
  }, [status, session, filter, router, fetchOrders]);

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

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-4">
          Admin Dashboard
        </h1>
        <div className="flex gap-2 flex-wrap">
          {(
            ["pending", "accepted", "completed", "rejected", "all"] as const
          ).map((status) => (
            <button
              key={status}
              onClick={() => setFilter(status)}
              className={`px-4 py-2 rounded-lg font-semibold transition capitalize ${
                filter === status
                  ? "bg-amber-600 text-white"
                  : "bg-gray-200 text-gray-700 hover:bg-gray-300"
              }`}
            >
              {status}
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="text-center py-12">
          <p className="text-gray-600">Loading orders...</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Orders List */}
          <div className="lg:col-span-2 space-y-4">
            {orders.length === 0 ? (
              <div className="bg-white rounded-lg shadow p-8 text-center">
                <p className="text-gray-600">No orders found</p>
              </div>
            ) : (
              orders.map((order) => (
                <button
                  key={order._id}
                  onClick={() => setSelectedOrder(order)}
                  className={`w-full text-left p-4 rounded-lg border-2 transition ${
                    selectedOrder?._id === order._id
                      ? "border-amber-600 bg-amber-50"
                      : "border-gray-200 bg-white hover:border-amber-300"
                  }`}
                >
                  <div className="flex justify-between items-start mb-2">
                    <div>
                      <h3 className="font-bold text-gray-900">
                        {order.orderNumber}
                      </h3>
                      <p className="text-sm text-gray-600">
                        {getCustomerName(order.customer)} -{" "}
                        {getCustomerEmail(order.customer)}
                      </p>
                    </div>
                    <span
                      className={`px-3 py-1 rounded-full text-xs font-semibold capitalize ${getStatusColor(
                        order.status
                      )}`}
                    >
                      {order.status}
                    </span>
                  </div>
                  <p className="text-sm text-gray-600">
                    {order.items?.length || 0} items - $
                    {order.totalPrice.toFixed(2)}
                  </p>
                </button>
              ))
            )}
          </div>

          {/* Order Details Panel */}
          {selectedOrder && (
            <div className="bg-white rounded-lg shadow p-6 h-fit">
              <h2 className="text-xl font-bold text-gray-900 mb-4">
                Order Details
              </h2>

              <div className="space-y-4 mb-6 pb-6 border-b">
                <div>
                  <p className="text-xs text-gray-600 uppercase">
                    Order Number
                  </p>
                  <p className="font-semibold text-gray-900">
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
            </div>
          )}
        </div>
      )}
    </div>
  );
}
