// Notification types for real-time events

export type NotificationType =
  | "NEW_ORDER"
  | "ORDER_ACCEPTED"
  | "ORDER_REJECTED"
  | "ORDER_COMPLETED"
  | "ORDER_CANCELLED"
  | "ORDER_READY";

export interface OrderNotification {
  type: NotificationType;
  orderId: string;
  restaurantID: string;
  customerName: string;
  customerPhone: string;
  itemCount: number;
  totalAmount: number;
  address?: string;
  timestamp: number;
  message: string;
}

export interface WebSocketMessage {
  action: "subscribe" | "unsubscribe" | "notify" | "ping" | "pong";
  channel?: "kitchen" | "customer";
  target?: string; // restaurantID for kitchen, customerId for customer
  data?: OrderNotification;
}
