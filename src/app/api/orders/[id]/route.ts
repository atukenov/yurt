import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { Order } from "@/models/Order";
import { getServerSession } from "next-auth/next";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string }> }
) {
  try {
    const { id } = await context.params;
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const order = await Order.findById(id)
      .populate("customer", "name email phone")
      .populate("location", "name address")
      .populate("items.menuItem", "name")
      .populate("items.toppings", "name price");

    if (!order) {
      return Response.json({ error: "Order not found" }, { status: 404 });
    }

    // Check authorization - customer can only see their own orders
    if (
      order.customer._id.toString() !== session.user.id &&
      session.user.role !== "admin"
    ) {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    return Response.json({ order });
  } catch (error) {
    console.error("Error fetching order:", error);
    return Response.json({ error: "Failed to fetch order" }, { status: 500 });
  }
}
