// Global notification manager using in-memory store and client subscriptions
// In production, this should use Redis for multi-instance deployments

import { OrderNotification } from "#types/notification";

interface ClientSubscription {
  clientId: string;
  type: "kitchen" | "customer";
  target: string; // restaurantID for kitchen, customerId for customer
  sendNotification: (notification: OrderNotification) => void;
}

class NotificationManager {
  private subscribers: Map<string, ClientSubscription> = new Map();
  private messageQueue: Map<string, OrderNotification[]> = new Map();

  subscribe(
    clientId: string,
    type: "kitchen" | "customer",
    target: string,
    sendNotification: (notification: OrderNotification) => void
  ) {
    const subscriptionKey = `${type}:${target}`;
    this.subscribers.set(clientId, {
      clientId,
      type,
      target,
      sendNotification,
    });

    // Send any queued messages for this subscription
    const queueKey = `${subscriptionKey}:${clientId}`;
    if (this.messageQueue.has(queueKey)) {
      const queued = this.messageQueue.get(queueKey) || [];
      queued.forEach((notification) => sendNotification(notification));
      this.messageQueue.delete(queueKey);
    }

    console.log(
      `[Notifications] Client ${clientId} subscribed to ${subscriptionKey}`
    );
  }

  unsubscribe(clientId: string) {
    this.subscribers.delete(clientId);
    console.log(`[Notifications] Client ${clientId} unsubscribed`);
  }

  broadcast(
    notification: OrderNotification,
    type: "kitchen" | "customer",
    target: string
  ) {
    const subscriptionKey = `${type}:${target}`;
    let sentCount = 0;

    this.subscribers.forEach((subscription, clientId) => {
      if (subscription.type === type && subscription.target === target) {
        try {
          subscription.sendNotification(notification);
          sentCount++;
        } catch (error) {
          console.error(`[Notifications] Error sending to ${clientId}:`, error);
          this.unsubscribe(clientId);
        }
      }
    });

    console.log(
      `[Notifications] Broadcast to ${type} [${target}]: ${sentCount} subscribers notified`
    );

    return sentCount;
  }

  notifyKitchen(restaurantID: string, notification: OrderNotification) {
    return this.broadcast(notification, "kitchen", restaurantID);
  }

  notifyCustomer(customerId: string, notification: OrderNotification) {
    return this.broadcast(notification, "customer", customerId);
  }

  getActiveSubscribersCount(): number {
    return this.subscribers.size;
  }

  getSubscribersForTarget(
    type: "kitchen" | "customer",
    target: string
  ): number {
    let count = 0;
    this.subscribers.forEach((subscription) => {
      if (subscription.type === type && subscription.target === target) {
        count++;
      }
    });
    return count;
  }
}

// Singleton instance
export const notificationManager = new NotificationManager();
