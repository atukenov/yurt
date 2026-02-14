import { Logger } from "@nestjs/common";
import {
  OnGatewayConnection,
  OnGatewayDisconnect,
  SubscribeMessage,
  WebSocketGateway,
  WebSocketServer,
} from "@nestjs/websockets";
import {
  SOCKET_EVENTS,
  SOCKET_NAMESPACE,
  SOCKET_ROOMS,
} from "@yurt/shared-config";
import { Server, Socket } from "socket.io";

@WebSocketGateway({
  namespace: SOCKET_NAMESPACE,
  cors: {
    origin: process.env.FRONTEND_URL || "http://localhost:3000",
    methods: ["GET", "POST"],
    credentials: true,
  },
  transports: ["websocket", "polling"],
})
export class OrdersGateway implements OnGatewayConnection, OnGatewayDisconnect {
  @WebSocketServer()
  server: Server;

  private readonly logger = new Logger(OrdersGateway.name);

  handleConnection(client: Socket) {
    this.logger.log(`Client connected: ${client.id}`);
  }

  handleDisconnect(client: Socket) {
    this.logger.log(`Client disconnected: ${client.id}`);
  }

  @SubscribeMessage(SOCKET_EVENTS.USER_JOIN)
  handleUserJoin(client: Socket, data: { userId: string; role: string }) {
    client.data.userId = data.userId;
    client.data.role = data.role;

    if (data.role === "admin") {
      client.join(SOCKET_ROOMS.ADMIN);
    } else {
      client.join(SOCKET_ROOMS.customer(data.userId));
    }

    this.logger.log(`User ${data.userId} (${data.role}) joined`);
  }

  // Helper methods for other modules to emit events
  emitOrderCreated(orderData: any) {
    this.server
      .to(SOCKET_ROOMS.ADMIN)
      .emit(SOCKET_EVENTS.ORDER_CREATED, orderData);
    if (orderData.userId) {
      this.server
        .to(SOCKET_ROOMS.customer(orderData.userId))
        .emit(SOCKET_EVENTS.ORDER_CREATED, orderData);
    }
  }

  emitOrderUpdated(orderId: string, orderData: any) {
    this.server
      .to(SOCKET_ROOMS.ADMIN)
      .emit(SOCKET_EVENTS.ORDER_UPDATED, { orderId, ...orderData });
    if (orderData.userId) {
      this.server
        .to(SOCKET_ROOMS.customer(orderData.userId))
        .emit(SOCKET_EVENTS.ORDER_UPDATED, { orderId, ...orderData });
    }
  }

  emitOrderStatusChanged(orderId: string, status: string, userId: string) {
    this.server
      .to(SOCKET_ROOMS.ADMIN)
      .emit(SOCKET_EVENTS.ORDER_STATUS_CHANGED, { orderId, status });
    this.server
      .to(SOCKET_ROOMS.customer(userId))
      .emit(SOCKET_EVENTS.ORDER_STATUS_CHANGED, { orderId, status });
  }

  emitOrderDeleted(orderId: string, userId: string) {
    this.server
      .to(SOCKET_ROOMS.ADMIN)
      .emit(SOCKET_EVENTS.ORDER_DELETED, { orderId });
    this.server
      .to(SOCKET_ROOMS.customer(userId))
      .emit(SOCKET_EVENTS.ORDER_DELETED, { orderId });
  }
}
