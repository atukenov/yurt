import { Server as HTTPServer } from "http";
import { Server as SocketIOServer } from "socket.io";

let ioInstance: SocketIOServer | null = null;

export function initializeSocket(httpServer: HTTPServer): SocketIOServer {
  if (ioInstance) {
    return ioInstance;
  }

  ioInstance = new SocketIOServer(httpServer, {
    cors: {
      origin: process.env.NEXTAUTH_URL || "http://localhost:3000",
      methods: ["GET", "POST"],
      credentials: true,
    },
    transports: ["websocket", "polling"],
  });

  // Setup namespaces for different event types
  ioInstance.of("/orders").on("connection", (socket) => {
    console.log(`[Orders Namespace] Client connected: ${socket.id}`);

    // Store user info when they join
    socket.on(
      "user-join",
      (data: { userId: string; role: "admin" | "customer" }) => {
        socket.data.userId = data.userId;
        socket.data.role = data.role;

        if (data.role === "admin") {
          socket.join("admin");
        } else {
          socket.join(`customer-${data.userId}`);
        }

        console.log(`[Orders] User ${data.userId} (${data.role}) joined`);
      }
    );

    socket.on("disconnect", () => {
      console.log(`[Orders Namespace] Client disconnected: ${socket.id}`);
    });
  });

  return ioInstance;
}

export function getSocket(): SocketIOServer | null {
  return ioInstance;
}

// Helper functions to emit events
export function emitOrderCreated(orderData: any) {
  if (!ioInstance) return;

  // Emit to all admins and to the customer who created the order
  ioInstance.of("/orders").to("admin").emit("order-created", orderData);
  ioInstance
    .of("/orders")
    .to(`customer-${orderData.userId}`)
    .emit("order-created", orderData);
}

export function emitOrderUpdated(orderId: string, orderData: any) {
  if (!ioInstance) return;

  // Emit to all admins and to the customer who owns the order
  ioInstance
    .of("/orders")
    .to("admin")
    .emit("order-updated", { orderId, ...orderData });
  ioInstance
    .of("/orders")
    .to(`customer-${orderData.userId}`)
    .emit("order-updated", { orderId, ...orderData });
}

export function emitOrderDeleted(orderId: string, userId: string) {
  if (!ioInstance) return;

  // Emit to all admins and to the customer who owned the order
  ioInstance.of("/orders").to("admin").emit("order-deleted", { orderId });
  ioInstance
    .of("/orders")
    .to(`customer-${userId}`)
    .emit("order-deleted", { orderId });
}

export function emitOrderStatusChanged(
  orderId: string,
  status: string,
  userId: string
) {
  if (!ioInstance) return;

  // Emit status change to both admin and customer
  ioInstance
    .of("/orders")
    .to("admin")
    .emit("order-status-changed", { orderId, status });
  ioInstance
    .of("/orders")
    .to(`customer-${userId}`)
    .emit("order-status-changed", { orderId, status });
}
