import { getServerSession } from "next-auth";
import { NextResponse } from "next/server";

import connectDB from "#utils/database/connect";
import { Orders, TOrder } from "#utils/database/models/order";
import { authOptions } from "#utils/helper/authHelper";
import { CatchNextResponse } from "#utils/helper/common";
import { notificationManager } from "#utils/helper/notificationManager";

export async function POST(req: Request) {
  try {
    const session = await getServerSession(authOptions);
    const { orderId, action } = await req.json();

    if (!session?.restaurant?.username) {
      throw { status: 401, message: "Authentication Required" };
    }

    if (!["accept", "reject", "complete"].includes(action)) {
      throw { status: 400, message: "Invalid action" };
    }

    await connectDB();

    const order = await Orders.findById<TOrder>(orderId).populate("customer");
    if (!order) {
      throw { status: 404, message: "Order not found" };
    }

    if (order.restaurantID !== session.restaurant.username) {
      throw { status: 403, message: "Unauthorized" };
    }

    // Update order state based on action
    const stateMap: Record<string, "active" | "reject" | "complete"> = {
      accept: "active",
      reject: "reject",
      complete: "complete",
    };

    order.state = stateMap[action];
    await order.save();

    // Notify customer about the update
    const notificationTypeMap: Record<string, string> = {
      accept: "ORDER_ACCEPTED",
      reject: "ORDER_REJECTED",
      complete: "ORDER_COMPLETED",
    };

    const messageMap: Record<string, string> = {
      accept: "Your order has been accepted",
      reject: "Your order has been rejected",
      complete: "Your order is ready for pickup!",
    };

    const customerId =
      typeof order.customer === "string" ? order.customer : order.customer._id;

    notificationManager.notifyCustomer(customerId.toString(), {
      type: notificationTypeMap[action] as any,
      orderId: orderId,
      restaurantID: order.restaurantID,
      customerName: "You",
      customerPhone: "",
      itemCount: order.products.length,
      totalAmount: order.products.reduce((sum, p) => {
        const price =
          typeof p.price === "string" ? parseFloat(p.price) : p.price;
        const tax = typeof p.tax === "string" ? parseFloat(p.tax) : p.tax;
        return sum + (price * p.quantity + tax);
      }, 0),
      timestamp: Date.now(),
      message: messageMap[action],
    });

    return NextResponse.json({
      status: 200,
      message: `Order ${action}ed successfully`,
      order,
    });
  } catch (err) {
    console.log(err);
    return CatchNextResponse(err);
  }
}

export const dynamic = "force-dynamic";
