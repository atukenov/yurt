import { authOptions } from "@/lib/auth";
import { errorLogger } from "@/lib/logger";
import { connectDB } from "@/lib/mongodb";
import { Order } from "@/models/Order";
import { getServerSession } from "next-auth/next";

export async function GET(request: Request) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    const { searchParams } = new URL(request.url);

    // Build filter query
    const filter: any = {};

    // Filter by status
    const status = searchParams.get("status");
    if (status && status !== "all") {
      filter.status = status;
    }

    // Filter by location
    const locationId = searchParams.get("locationId");
    if (locationId) {
      filter.location = locationId;
    }

    // Filter by payment method
    const paymentMethod = searchParams.get("paymentMethod");
    if (paymentMethod) {
      filter.paymentMethod = paymentMethod;
    }

    // Search by order ID or customer name/email
    const searchQuery = searchParams.get("searchQuery");
    if (searchQuery) {
      const searchRegex = { $regex: searchQuery, $options: "i" };
      filter.$or = [
        { orderNumber: searchRegex },
        { "customer.name": searchRegex },
        { "customer.email": searchRegex },
      ];
    }

    // Filter by date range
    const startDate = searchParams.get("startDate");
    const endDate = searchParams.get("endDate");
    if (startDate || endDate) {
      filter.createdAt = {};
      if (startDate) {
        // Set to start of day
        const start = new Date(startDate);
        start.setHours(0, 0, 0, 0);
        filter.createdAt.$gte = start;
      }
      if (endDate) {
        // Set to end of day
        const end = new Date(endDate);
        end.setHours(23, 59, 59, 999);
        filter.createdAt.$lte = end;
      }
    }

    // Get sort and pagination
    const sortBy = searchParams.get("sortBy") || "createdAt";
    const sortOrder = searchParams.get("sortOrder") === "asc" ? 1 : -1;
    const page = parseInt(searchParams.get("page") || "1");
    const limit = parseInt(searchParams.get("limit") || "20");
    const skip = (page - 1) * limit;

    // Fetch orders with filters
    const orders = await Order.find(filter)
      .populate("customer", "name email phone")
      .populate("location", "name address city")
      .populate("items.menuItem", "name basePrice")
      .populate("items.toppings", "name price")
      .sort({ [sortBy]: sortOrder })
      .skip(skip)
      .limit(limit)
      .lean();

    // Get total count for pagination
    const total = await Order.countDocuments(filter);

    errorLogger.info("Admin fetched filtered orders", {
      userId: session.user.id,
      filters: { status, locationId, paymentMethod, searchQuery },
      resultCount: orders.length,
      total,
    });

    return Response.json({
      orders,
      pagination: {
        total,
        page,
        limit,
        pages: Math.ceil(total / limit),
      },
    });
  } catch (error) {
    const message =
      error instanceof Error ? error.message : "Failed to fetch orders";
    errorLogger.error(
      "Error fetching admin orders",
      {},
      error instanceof Error ? error : new Error(message)
    );
    return Response.json({ error: message }, { status: 500 });
  }
}
