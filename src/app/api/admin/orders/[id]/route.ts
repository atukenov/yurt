import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { emitOrderStatusChanged, emitOrderUpdated } from "@/lib/socket";
import { Notification } from "@/models/Notification";
import { Order } from "@/models/Order";
import { getServerSession } from "next-auth/next";
import { z } from "zod";

const updateOrderSchema = z.object({
  status: z
    .enum(["pending", "accepted", "rejected", "completed", "cancelled"])
    .optional(),
  estimatedPrepTime: z.number().optional(),
  rejectionReason: z
    .enum([
      "no_milk",
      "no_coffee_beans",
      "size_unavailable",
      "equipment_issue",
      "custom",
    ])
    .optional(),
  rejectionComment: z.string().optional(),
});

export async function PUT(
  request: Request,
  context: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await context.params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    const body = await request.json();
    const validation = updateOrderSchema.safeParse(body);

    if (!validation.success) {
      return Response.json({ error: "Invalid input" }, { status: 400 });
    }

    const order = await Order.findByIdAndUpdate(id, validation.data, {
      new: true,
    }).populate("customer");

    if (!order) {
      return Response.json({ error: "Order not found" }, { status: 404 });
    }

    // Create notification for customer when order status changes
    const { status } = validation.data;

    // Get customer ID - handle both populated object and string ID
    const customerId =
      typeof order.customer === "object" && order.customer
        ? order.customer._id
        : order.customer;

    if (status === "accepted" && customerId) {
      await Notification.create({
        order: order._id,
        recipient: customerId,
        type: "order_accepted",
        message: `Your order ${order.orderNumber} has been accepted. Estimated time: ${validation.data.estimatedPrepTime} minutes`,
      });
    } else if (status === "rejected" && customerId) {
      await Notification.create({
        order: order._id,
        recipient: customerId,
        type: "order_rejected",
        message: `Your order ${order.orderNumber} has been rejected. Reason: ${validation.data.rejectionReason}`,
      });
    } else if (status === "completed" && customerId) {
      await Notification.create({
        order: order._id,
        recipient: customerId,
        type: "order_completed",
        message: `Your order ${order.orderNumber} is ready for pickup!`,
      });
    }

    // Emit Socket.io events for real-time updates
    const populatedOrder = await Order.findById(order._id).populate([
      { path: "customer", select: "name email phone" },
      { path: "location", select: "name address city" },
      { path: "items.menuItem", select: "name basePrice" },
    ]);

    if (populatedOrder) {
      emitOrderUpdated(id, populatedOrder.toObject());
      if (status) {
        emitOrderStatusChanged(id, status, customerId?.toString() || "");
      }
    }

    return Response.json({ order });
  } catch (error) {
    console.error("Error updating order:", error);
    return Response.json({ error: "Failed to update order" }, { status: 500 });
  }
}
