import { authOptions } from "@/lib/auth";
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

    // Filter by customer
    const customerId = searchParams.get("customerId");
    if (customerId) {
      filter.customer = customerId;
    }

    // Filter by date range
    const startDate = searchParams.get("startDate");
    const endDate = searchParams.get("endDate");
    if (startDate || endDate) {
      filter.createdAt = {};
      if (startDate) {
        filter.createdAt.$gte = new Date(startDate);
      }
      if (endDate) {
        filter.createdAt.$lte = new Date(endDate);
      }
    }

    // Filter by payment status
    const paymentStatus = searchParams.get("paymentStatus");
    if (paymentStatus) {
      filter.paymentStatus = paymentStatus;
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
      .sort({ [sortBy]: sortOrder })
      .skip(skip)
      .limit(limit);

    // Get total count for pagination
    const total = await Order.countDocuments(filter);

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
    console.error("Error fetching orders:", error);
    return Response.json({ error: "Failed to fetch orders" }, { status: 500 });
  }
}
