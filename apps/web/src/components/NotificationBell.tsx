"use client";

import { apiClient } from "@/lib/api-client";
import { useEffect, useState } from "react";
import { useToastNotification } from "./ToastProvider";

interface Notification {
  _id: string;
  message: string;
  type: "order_accepted" | "order_rejected" | "order_completed";
  read: boolean;
  order: { orderNumber: string };
  createdAt: string;
}

export function NotificationBell() {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [showDropdown, setShowDropdown] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const [previousCount, setPreviousCount] = useState(0);
  const { showToast } = useToastNotification();

  useEffect(() => {
    fetchNotifications();
    const interval = setInterval(fetchNotifications, 3000);
    return () => clearInterval(interval);
  }, []);

  const fetchNotifications = async () => {
    try {
      const data = await apiClient.get("/notifications");
      const newUnreadCount = data.notifications.filter(
        (n: any) => !n.read
      ).length;

      // Show toast for new unread notifications
      if (newUnreadCount > previousCount) {
        const newNotifications = data.notifications.slice(
          0,
          newUnreadCount - previousCount
        );
        newNotifications.forEach(async (notif: Notification) => {
          const typeEmoji = {
            order_accepted: "✅",
            order_rejected: "❌",
            order_completed: "🎉",
          }[notif.type];

          showToast(
            `${typeEmoji} ${notif.order.orderNumber}: ${notif.message}`,
            notif.type === "order_rejected" ? "error" : "success"
          );

          // Mark notification as read immediately after showing
          try {
            await apiClient.put(`/notifications/${notif._id}`);
          } catch (err) {
            console.error("Error marking notification as read:", err);
          }
        });
      }

      setUnreadCount(newUnreadCount);
      setPreviousCount(newUnreadCount);
    } catch (error) {
      console.error("Error fetching notifications:", error);
    }
  };

  const markAsRead = async (id: string) => {
    try {
      await apiClient.put(`/notifications/${id}`);
      fetchNotifications();
    } catch (error) {
      console.error("Error marking notification as read:", error);
    }
  };

  const getTypeColor = (type: string) => {
    switch (type) {
      case "order_accepted":
        return "bg-blue-50 border-l-4 border-blue-500";
      case "order_rejected":
        return "bg-red-50 border-l-4 border-red-500";
      case "order_completed":
        return "bg-green-50 border-l-4 border-green-500";
      default:
        return "bg-gray-50 border-l-4 border-gray-500";
    }
  };

  return (
    <div className="relative">
      <button
        onClick={() => setShowDropdown(!showDropdown)}
        className="relative p-2 text-gray-600 hover:text-gray-900"
      >
        🔔
        {unreadCount > 0 && (
          <span className="absolute top-0 right-0 inline-flex items-center justify-center px-2 py-1 text-xs font-bold leading-none text-white transform translate-x-1/2 -translate-y-1/2 bg-red-600 rounded-full">
            {unreadCount}
          </span>
        )}
      </button>

      {showDropdown && (
        <div className="absolute right-0 mt-2 w-96 bg-white rounded-lg shadow-xl z-50">
          <div className="max-h-96 overflow-y-auto">
            {notifications.length === 0 ? (
              <div className="p-4 text-center text-gray-600">
                No notifications
              </div>
            ) : (
              notifications.map((notif) => (
                <div
                  key={notif._id}
                  className={`p-4 border-b cursor-pointer transition ${getTypeColor(
                    notif.type
                  )} ${notif.read ? "opacity-60" : "opacity-100"}`}
                  onClick={() => markAsRead(notif._id)}
                >
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <p className="font-semibold text-gray-900">
                        {notif.order.orderNumber}
                      </p>
                      <p className="text-sm text-gray-700 mt-1">
                        {notif.message}
                      </p>
                      <p className="text-xs text-gray-500 mt-2">
                        {new Date(notif.createdAt).toLocaleTimeString()}
                      </p>
                    </div>
                    {!notif.read && (
                      <div className="w-2 h-2 bg-blue-500 rounded-full mt-1"></div>
                    )}
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  );
}
