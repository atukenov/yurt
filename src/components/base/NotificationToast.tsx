"use client";

import type { NotificationType, OrderNotification } from "#types/notification";
import { useNotifications } from "#utils/hooks/useNotifications";
import { useEffect } from "react";
import { toast } from "react-toastify";

const notificationConfig: Record<
  NotificationType,
  { title: string; icon: string }
> = {
  NEW_ORDER: { title: "🆕 New Order", icon: "📦" },
  ORDER_ACCEPTED: { title: "✅ Order Accepted", icon: "✔️" },
  ORDER_REJECTED: { title: "❌ Order Rejected", icon: "✖️" },
  ORDER_COMPLETED: { title: "🎉 Order Ready", icon: "🍽️" },
  ORDER_CANCELLED: { title: "⏹️ Order Cancelled", icon: "🚫" },
  ORDER_READY: { title: "🔔 Order Ready for Pickup", icon: "📢" },
};

export function NotificationToast() {
  const { notifications, clearNotification } = useNotifications();

  useEffect(() => {
    notifications.forEach((notification: OrderNotification) => {
      const config = notificationConfig[notification.type];

      let message = `${config.icon} ${notification.message}`;

      if (notification.type === "NEW_ORDER") {
        message = `${config.icon} ${notification.customerName} ordered ${notification.itemCount} item(s) - $${notification.totalAmount}`;
      }

      const toastType =
        notification.type === "ORDER_REJECTED" ||
        notification.type === "ORDER_CANCELLED"
          ? "error"
          : notification.type === "NEW_ORDER"
            ? "info"
            : "success";

      toast[toastType](message, {
        position: "top-right",
        autoClose: 5000,
        hideProgressBar: false,
        closeOnClick: true,
        pauseOnHover: true,
        draggable: true,
      });

      // Auto-clear after showing
      setTimeout(() => {
        clearNotification(notification.orderId);
      }, 6000);
    });
  }, [notifications, clearNotification]);

  return null;
}
