"use client";

import { OrderNotification } from "#types/notification";
import {
  createContext,
  ReactNode,
  useCallback,
  useEffect,
  useRef,
  useState,
} from "react";

interface NotificationContextType {
  notifications: OrderNotification[];
  addNotification: (notification: OrderNotification) => void;
  clearNotification: (orderId: string) => void;
  clearAll: () => void;
  isConnected: boolean;
}

export const NotificationContext = createContext<
  NotificationContextType | undefined
>(undefined);

interface NotificationProviderProps {
  children: ReactNode;
  type: "kitchen" | "customer";
  target: string; // restaurantID for kitchen, customerId for customer
}

export function NotificationProvider({
  children,
  type,
  target,
}: NotificationProviderProps) {
  const [notifications, setNotifications] = useState<OrderNotification[]>([]);
  const [isConnected, setIsConnected] = useState(false);
  const eventSourceRef = useRef<EventSource | null>(null);
  const clientIdRef = useRef<string>(
    `${type}-${target}-${Date.now()}-${Math.random()}`
  );

  const addNotification = useCallback((notification: OrderNotification) => {
    setNotifications((prev) => [notification, ...prev]);
  }, []);

  const clearNotification = useCallback((orderId: string) => {
    setNotifications((prev) => prev.filter((n) => n.orderId !== orderId));
  }, []);

  const clearAll = useCallback(() => {
    setNotifications([]);
  }, []);

  useEffect(() => {
    const connect = () => {
      const params = new URLSearchParams({
        type,
        target,
        clientId: clientIdRef.current,
      });

      const eventSource = new EventSource(
        `/api/notifications/subscribe?${params}`
      );

      eventSource.onopen = () => {
        console.log(`[Notifications] Connected: ${type} - ${target}`);
        setIsConnected(true);
      };

      eventSource.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);

          // Ignore heartbeats and connection messages
          if (data.type === "CONNECTION" || event.data === ": heartbeat") {
            return;
          }

          if (data.orderId && data.type) {
            addNotification(data as OrderNotification);
          }
        } catch (error) {
          console.error("[Notifications] Error parsing message:", error);
        }
      };

      eventSource.onerror = (error) => {
        console.error("[Notifications] Connection error:", error);
        setIsConnected(false);
        eventSource.close();

        // Reconnect after 5 seconds
        setTimeout(() => {
          connect();
        }, 5000);
      };

      eventSourceRef.current = eventSource;
    };

    connect();

    return () => {
      if (eventSourceRef.current) {
        eventSourceRef.current.close();
        setIsConnected(false);
      }
    };
  }, [type, target, addNotification]);

  return (
    <NotificationContext.Provider
      value={{
        notifications,
        addNotification,
        clearNotification,
        clearAll,
        isConnected,
      }}
    >
      {children}
    </NotificationContext.Provider>
  );
}
