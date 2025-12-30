"use client";

import { useSession } from "next-auth/react";
import React, { createContext, useContext, useEffect, useState } from "react";
import { io, Socket } from "socket.io-client";

interface SocketContextType {
  socket: Socket | null;
  isConnected: boolean;
  isAvailable: boolean;
  orderEvents: {
    orderCreated?: (callback: (data: any) => void) => void;
    orderUpdated?: (callback: (data: any) => void) => void;
    orderDeleted?: (callback: (data: any) => void) => void;
    orderStatusChanged?: (callback: (data: any) => void) => void;
  };
}

const SocketContext = createContext<SocketContextType>({
  socket: null,
  isConnected: false,
  isAvailable: false,
  orderEvents: {},
});

export function SocketProvider({ children }: { children: React.ReactNode }) {
  const [socket, setSocket] = useState<Socket | null>(null);
  const [isConnected, setIsConnected] = useState(false);
  const [isAvailable, setIsAvailable] = useState(false);
  const { data: session } = useSession();

  useEffect(() => {
    // Only connect if user is authenticated
    if (!session?.user) {
      setSocket(null);
      setIsConnected(false);
      return;
    }

    try {
      // Try to connect to socket.io server
      const newSocket = io({
        // Use default path
        reconnection: true,
        reconnectionDelay: 1000,
        reconnectionDelayMax: 5000,
        reconnectionAttempts: 3,
        transports: ["websocket", "polling"],
      });

      // Register connection handlers
      newSocket.on("connect", () => {
        console.log("[Socket] Connected to server");
        setIsConnected(true);
        setIsAvailable(true);

        // Emit user join event with authentication data
        if (session.user?.id) {
          newSocket.emit("user-join", {
            userId: session.user.id,
            role: session.user.role || "customer",
          });
        }
      });

      newSocket.on("disconnect", () => {
        console.log("[Socket] Disconnected from server");
        setIsConnected(false);
      });

      newSocket.on("connect_error", (error: any) => {
        console.warn(
          "[Socket] Connection error - using polling fallback:",
          error.message || error
        );
        setIsAvailable(false);
      });

      newSocket.on("error", (error: any) => {
        console.warn("[Socket] Socket error:", error);
        setIsAvailable(false);
      });

      setSocket(newSocket);

      return () => {
        newSocket.disconnect();
      };
    } catch (error) {
      console.warn("[Socket] Failed to initialize Socket.io:", error);
      setIsAvailable(false);
      setSocket(null);
    }
  }, [session]);

  const orderEvents = {
    orderCreated: (callback: (data: any) => void) => {
      if (socket && isAvailable) {
        socket.off("order-created");
        socket.on("order-created", callback);
      }
    },
    orderUpdated: (callback: (data: any) => void) => {
      if (socket && isAvailable) {
        socket.off("order-updated");
        socket.on("order-updated", callback);
      }
    },
    orderDeleted: (callback: (data: any) => void) => {
      if (socket && isAvailable) {
        socket.off("order-deleted");
        socket.on("order-deleted", callback);
      }
    },
    orderStatusChanged: (callback: (data: any) => void) => {
      if (socket && isAvailable) {
        socket.off("order-status-changed");
        socket.on("order-status-changed", callback);
      }
    },
  };

  return (
    <SocketContext.Provider
      value={{ socket, isConnected, isAvailable, orderEvents }}
    >
      {children}
    </SocketContext.Provider>
  );
}

export function useSocket() {
  const context = useContext(SocketContext);
  if (!context) {
    throw new Error("useSocket must be used within SocketProvider");
  }
  return context;
}
