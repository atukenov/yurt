"use client";

import Button from "#components/base/Button";
import { NotificationBell } from "#components/base/NotificationBell";
import { NotificationToast } from "#components/base/NotificationToast";
import { NotificationProvider } from "#components/context/Notification";
import { useNotifications } from "#utils/hooks/useNotifications";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { toast } from "react-toastify";
import "./kitchen.scss";

interface Order {
  _id: string;
  restaurantID: string;
  customer: {
    _id: string;
    phone: string;
    name?: string;
  };
  products: Array<{
    product: { name: string; description?: string };
    quantity: number;
    price: string;
  }>;
  state: "active" | "reject" | "cancel" | "complete";
  createdAt: string;
  address?: string;
}

function KitchenContent() {
  const { data: session } = useSession();
  const router = useRouter();
  const { notifications } = useNotifications();
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<"active" | "all">("active");

  // Redirect if not admin
  useEffect(() => {
    if (
      session?.user?.role !== "restaurant" &&
      session?.user?.role !== "kitchen"
    ) {
      router.push("/");
    }
  }, [session, router]);

  // Fetch orders
  const fetchOrders = async () => {
    try {
      const res = await fetch("/api/order");
      if (!res.ok) throw new Error("Failed to fetch orders");
      const data = await res.json();
      setOrders(data || []);
    } catch (error) {
      console.error("Error fetching orders:", error);
      toast.error("Failed to load orders");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchOrders();
    // Refresh orders every 5 seconds
    const interval = setInterval(fetchOrders, 5000);
    return () => clearInterval(interval);
  }, []);

  const handleOrderAction = async (
    orderId: string,
    action: "accept" | "reject" | "complete"
  ) => {
    try {
      const res = await fetch("/api/order/updateStatus", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ orderId, action }),
      });

      if (!res.ok) throw new Error("Failed to update order");

      toast.success(`Order ${action}ed!`);
      fetchOrders();
    } catch (error) {
      console.error("Error updating order:", error);
      toast.error(`Failed to ${action} order`);
    }
  };

  const filteredOrders = orders.filter((order) => {
    if (filter === "active") {
      return order.state === "active";
    }
    return true;
  });

  const activeCount = orders.filter((o) => o.state === "active").length;
  const completedCount = orders.filter((o) => o.state === "complete").length;

  return (
    <div className="kitchen-page">
      <header className="kitchen-header">
        <h1>🍳 Kitchen Dashboard</h1>
        <div className="header-right">
          <NotificationBell />
        </div>
      </header>

      <NotificationToast />

      <div className="kitchen-stats">
        <div className="stat-card">
          <div className="stat-number">{activeCount}</div>
          <div className="stat-label">Active Orders</div>
        </div>
        <div className="stat-card">
          <div className="stat-number">{completedCount}</div>
          <div className="stat-label">Completed</div>
        </div>
        <div className="stat-card">
          <div className="stat-number">{notifications.length}</div>
          <div className="stat-label">New Notifications</div>
        </div>
      </div>

      <div className="kitchen-filters">
        <Button
          label="Active Orders"
          type={filter === "active" ? "primary" : "ghost"}
          onClick={() => setFilter("active")}
        />
        <Button
          label="All Orders"
          type={filter === "all" ? "primary" : "ghost"}
          onClick={() => setFilter("all")}
        />
      </div>

      <div className="kitchen-orders">
        {loading ? (
          <div className="loading">Loading orders...</div>
        ) : filteredOrders.length === 0 ? (
          <div className="no-orders">
            <p>No orders to display</p>
          </div>
        ) : (
          filteredOrders.map((order) => (
            <div key={order._id} className={`order-card order-${order.state}`}>
              <div className="order-header">
                <div className="order-id">
                  #{order._id.slice(-6).toUpperCase()}
                </div>
                <div className={`order-status status-${order.state}`}>
                  {order.state.toUpperCase()}
                </div>
              </div>

              <div className="order-customer">
                <div className="customer-name">
                  {order.customer.name || "Customer"}
                </div>
                <div className="customer-phone">📞 {order.customer.phone}</div>
                {order.address && (
                  <div className="customer-address">📍 {order.address}</div>
                )}
              </div>

              <div className="order-items">
                <h4>Items:</h4>
                <ul>
                  {order.products.map((item, idx) => (
                    <li key={idx}>
                      {item.quantity}x {item.product.name}
                    </li>
                  ))}
                </ul>
              </div>

              <div className="order-time">
                {new Date(order.createdAt).toLocaleTimeString()}
              </div>

              {order.state === "active" && (
                <div className="order-actions">
                  <Button
                    label="Accept"
                    type="primary"
                    onClick={() => handleOrderAction(order._id, "accept")}
                  />
                  <Button
                    label="Reject"
                    type="secondary"
                    onClick={() => handleOrderAction(order._id, "reject")}
                  />
                  <Button
                    label="Complete"
                    type="primary"
                    onClick={() => handleOrderAction(order._id, "complete")}
                  />
                </div>
              )}

              {order.state === "complete" && (
                <div className="order-completed">✅ Order Completed</div>
              )}

              {order.state === "reject" && (
                <div className="order-rejected">❌ Order Rejected</div>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}

export default function Kitchen() {
  const { data: session } = useSession();

  if (!session || !session.user) {
    return <div>Loading...</div>;
  }

  return (
    <NotificationProvider
      type="kitchen"
      target={session.user.restaurantID || "unknown"}
    >
      <KitchenContent />
    </NotificationProvider>
  );
}
