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
      // Try to connect to socket.io server with proper configuration
      const socketUrl =
        typeof window !== "undefined"
          ? window.location.origin
          : "http://localhost:3000";

      const newSocket = io(socketUrl, {
        path: "/socket.io/",
        reconnection: true,
        reconnectionDelay: 1000,
        reconnectionDelayMax: 10000,
        reconnectionAttempts: 5,
        timeout: 30000, // 30 second timeout before falling back to polling
        transports: ["websocket", "polling"],
        autoConnect: true,
        secure: window?.location?.protocol === "https:",
        rejectUnauthorized: false,
      });

      // Register connection handlers
      newSocket.on("connect", () => {
        console.log("[Socket] ✓ Connected to server");
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

      newSocket.on("disconnect", (reason: string) => {
        console.log(`[Socket] ✗ Disconnected from server: ${reason}`);
        setIsConnected(false);
      });

      newSocket.on("connect_error", (error: any) => {
        const errorMsg = error?.message || error?.toString() || "Unknown error";
        console.warn(
          `[Socket] ⚠ Connection error (falling back to polling): ${errorMsg}`
        );
        // Don't set isAvailable to false immediately - polling will work
        setIsConnected(false);
      });

      newSocket.on("error", (error: any) => {
        const errorMsg = error?.message || error?.toString() || "Unknown error";
        console.warn(`[Socket] ⚠ Socket error: ${errorMsg}`);
        // Keep attempting reconnection even on errors
      });

      // Log transport upgrade/downgrade
      newSocket.on("upgrade", (transport: string) => {
        console.log(`[Socket] → Transport upgraded to: ${transport}`);
      });

      newSocket.on("downgrade", (transport: string) => {
        console.log(`[Socket] ↓ Transport downgraded to: ${transport}`);
      });

      setSocket(newSocket);

      // Proper cleanup on unmount
      return () => {
        console.log("[Socket] Cleaning up socket connection");
        newSocket.disconnect();
        setSocket(null);
        setIsConnected(false);
      };
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error);
      console.error("[Socket] Failed to initialize Socket.io:", errorMsg);
      setIsAvailable(false);
      setSocket(null);
    }
  }, [session]);

  const orderEvents = {
    orderCreated: (callback: (data: any) => void) => {
      if (socket) {
        // Remove any existing listeners to prevent duplicates
        socket.off("order-created");
        socket.on("order-created", (data: any) => {
          try {
            callback(data);
          } catch (error) {
            console.error("[Socket] Error in orderCreated callback:", error);
          }
        });
      }
    },
    orderUpdated: (callback: (data: any) => void) => {
      if (socket) {
        socket.off("order-updated");
        socket.on("order-updated", (data: any) => {
          try {
            callback(data);
          } catch (error) {
            console.error("[Socket] Error in orderUpdated callback:", error);
          }
        });
      }
    },
    orderDeleted: (callback: (data: any) => void) => {
      if (socket) {
        socket.off("order-deleted");
        socket.on("order-deleted", (data: any) => {
          try {
            callback(data);
          } catch (error) {
            console.error("[Socket] Error in orderDeleted callback:", error);
          }
        });
      }
    },
    orderStatusChanged: (callback: (data: any) => void) => {
      if (socket) {
        socket.off("order-status-changed");
        socket.on("order-status-changed", (data: any) => {
          try {
            callback(data);
          } catch (error) {
            console.error(
              "[Socket] Error in orderStatusChanged callback:",
              error
            );
          }
        });
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
