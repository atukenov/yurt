import { authOptions } from "@/lib/auth";
import { isLocationOpen } from "@/lib/locationAvailability";
import { connectDB } from "@/lib/mongodb";
import { emitOrderCreated } from "@/lib/socket";
import { Location, Loyalty, Notification, Order, User } from "@/models";
import { getServerSession } from "next-auth/next";
import { z } from "zod";

const createOrderSchema = z.object({
  locationId: z.string(),
  items: z.array(
    z.object({
      menuItemId: z.string(),
      quantity: z.number().min(1),
      size: z.enum(["small", "medium", "large"]),
      toppings: z.array(z.string()).optional(),
      specialInstructions: z.string().optional(),
    }),
  ),
  totalPrice: z.number().min(0),
  paymentMethod: z.enum(["kaspi", "applepay"]).default("kaspi"),
  notes: z.string().optional(),
});

export async function POST(request: Request) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const body = await request.json();
    const validation = createOrderSchema.safeParse(body);

    if (!validation.success) {
      return Response.json({ error: "Invalid input" }, { status: 400 });
    }

    const { locationId, items, totalPrice, paymentMethod, notes } =
      validation.data;

    // Check if location is open
    const location = await Location.findById(locationId);
    if (!location) {
      return Response.json({ error: "Location not found" }, { status: 404 });
    }

    const availability = isLocationOpen(location);
    if (!availability.isOpen) {
      return Response.json(
        {
          error: availability.reason || "Location is currently closed",
          isLocationClosed: true,
        },
        { status: 400 },
      );
    }

    // Generate order number
    const orderNumber = `ORD-${Date.now()}-${Math.random()
      .toString(36)
      .substr(2, 5)
      .toUpperCase()}`;

    // Create order
    const order = await Order.create({
      orderNumber,
      customer: session.user.id,
      location: locationId,
      items: items.map((item) => ({
        menuItem: item.menuItemId,
        quantity: item.quantity,
        size: item.size,
        toppings: item.toppings || [],
        specialInstructions: item.specialInstructions,
        priceAtOrder: totalPrice / items.length, // Simplified pricing
      })),
      totalPrice,
      paymentMethod,
      notes,
      paymentStatus: "pending",
      status: "pending",
    });

    // Create notification for all admins
    const adminUsers = await User.find({ role: "admin" });
    for (const admin of adminUsers) {
      await Notification.create({
        order: order._id,
        recipient: admin._id,
        type: "order_accepted",
        message: `New order ${orderNumber} received`,
      });
    }

    // Emit Socket.io event to notify admins and customer in real-time
    const populatedOrder = await order.populate([
      { path: "customer", select: "name email phone" },
      { path: "location", select: "name address city" },
      { path: "items.menuItem", select: "name basePrice" },
    ]);

    emitOrderCreated({
      ...populatedOrder.toObject(),
      userId: session.user.id,
    });

    // Award loyalty points after successful order creation
    try {
      const user = await User.findById(session.user.id);
      if (user) {
        let loyaltyRecord = await Loyalty.findOne({ user: session.user.id });

        if (!loyaltyRecord) {
          loyaltyRecord = new Loyalty({ user: session.user.id });
        }

        const pointsEarned = Math.floor(
          totalPrice * (loyaltyRecord.getTierBenefits().multiplier || 1),
        );

        loyaltyRecord.pointsHistory.push({
          type: "earned",
          points: pointsEarned,
          orderId: order._id.toString(),
          description: `Order ${orderNumber}`,
          createdAt: new Date(),
        });

        loyaltyRecord.availablePoints =
          (loyaltyRecord.availablePoints || 0) + pointsEarned;
        loyaltyRecord.totalPoints =
          (loyaltyRecord.totalPoints || 0) + pointsEarned;
        loyaltyRecord.totalSpent = (loyaltyRecord.totalSpent || 0) + totalPrice;
        loyaltyRecord.orderCount = (loyaltyRecord.orderCount || 0) + 1;
        loyaltyRecord.lastOrderDate = new Date();

        // Recalculate tier based on total points
        loyaltyRecord.calculateTier();

        await loyaltyRecord.save();
      }
    } catch (loyaltyError) {
      console.error("Error awarding loyalty points:", loyaltyError);
      // Don't fail the order if loyalty points error - log and continue
    }

    return Response.json(
      {
        message: "Order created successfully",
        order: {
          id: order._id,
          orderNumber: order.orderNumber,
          status: order.status,
        },
      },
      { status: 201 },
    );
  } catch (error) {
    console.error("Order creation error:", error);
    return Response.json({ error: "Failed to create order" }, { status: 500 });
  }
}

export async function GET(request: Request) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id) {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const orders = await Order.find({ customer: session.user.id })
      .populate("location", "name address")
      .populate("items.menuItem", "name")
      .sort({ createdAt: -1 })
      .limit(20);

    return Response.json({ orders });
  } catch (error) {
    console.error("Error fetching orders:", error);
    return Response.json({ error: "Failed to fetch orders" }, { status: 500 });
  }
}
