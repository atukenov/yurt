import { OrderNotification } from "#types/notification";
import { notificationManager } from "#utils/helper/notificationManager";
import { NextRequest, NextResponse } from "next/server";

// SSE connection endpoint for clients to subscribe to notifications
export async function GET(req: NextRequest) {
  const clientType = req.nextUrl.searchParams.get("type"); // 'kitchen' | 'customer'
  const target = req.nextUrl.searchParams.get("target"); // restaurantID or customerId
  const clientId = req.nextUrl.searchParams.get("clientId");

  if (!clientType || !target || !clientId) {
    return NextResponse.json(
      { error: "Missing required parameters: type, target, clientId" },
      { status: 400 }
    );
  }

  if (clientType !== "kitchen" && clientType !== "customer") {
    return NextResponse.json(
      { error: 'Invalid type. Must be "kitchen" or "customer"' },
      { status: 400 }
    );
  }

  // Set up SSE response
  const encoder = new TextEncoder();
  const customReadable = new ReadableStream({
    start(controller) {
      // Send initial connection confirmation
      const confirmMessage = {
        type: "CONNECTION",
        message: `Connected to ${clientType} notifications for ${target}`,
        timestamp: Date.now(),
      };
      controller.enqueue(
        encoder.encode(`data: ${JSON.stringify(confirmMessage)}\n\n`)
      );

      // Subscribe to notifications
      const sendNotification = (notification: OrderNotification) => {
        try {
          controller.enqueue(
            encoder.encode(`data: ${JSON.stringify(notification)}\n\n`)
          );
        } catch (error) {
          console.error("Error writing to SSE stream:", error);
          controller.close();
        }
      };

      notificationManager.subscribe(
        clientId,
        clientType as "kitchen" | "customer",
        target,
        sendNotification
      );

      // Send heartbeat every 30 seconds to keep connection alive
      const heartbeatInterval = setInterval(() => {
        try {
          controller.enqueue(encoder.encode(": heartbeat\n\n"));
        } catch (error) {
          clearInterval(heartbeatInterval);
          controller.close();
        }
      }, 30000);

      // Cleanup on client disconnect
      if (req.signal) {
        req.signal.addEventListener("abort", () => {
          clearInterval(heartbeatInterval);
          notificationManager.unsubscribe(clientId);
          controller.close();
        });
      }
    },
  });

  return new NextResponse(customReadable, {
    headers: {
      "Content-Type": "text/event-stream",
      "Cache-Control": "no-cache",
      Connection: "keep-alive",
      "X-Accel-Buffering": "no",
    },
  });
}

export const dynamic = "force-dynamic";
