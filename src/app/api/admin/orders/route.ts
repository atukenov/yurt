import { authOptions } from "@/lib/auth";
import { errorLogger } from "@/lib/logger";
import { connectDB } from "@/lib/mongodb";
import { Order } from "@/models/Order";
import { getServerSession } from "next-auth/next";
import { NextRequest } from "next/server";

export async function GET(request: NextRequest) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 403 });
    }

    // Use request.nextUrl for proper query parameter extraction in Next.js App Router
    const searchParams = request.nextUrl.searchParams;

    // Build filter query - only by date range
    const filter: any = {};

    // Filter by date range
    const startDate = searchParams.get("startDate");
    const endDate = searchParams.get("endDate");

    console.log("[DEBUG] Fetching orders for date range:", {
      startDate,
      endDate,
    });

    if (startDate || endDate) {
      filter.createdAt = {};
      if (startDate) {
        // Parse date and set to start of day in UTC
        const start = new Date(startDate + "T00:00:00Z");
        filter.createdAt.$gte = start;
        console.log("[DEBUG] Start date filter:", start);
      }
      if (endDate) {
        // Parse date and set to end of day in UTC
        const end = new Date(endDate + "T23:59:59Z");
        filter.createdAt.$lte = end;
        console.log("[DEBUG] End date filter:", end);
      }
    }

    // Get sort and pagination
    const sortBy = searchParams.get("sortBy") || "createdAt";
    const sortOrder = searchParams.get("sortOrder") === "asc" ? 1 : -1;
    const page = parseInt(searchParams.get("page") || "1");
    const limit = parseInt(searchParams.get("limit") || "20");
    const skip = (page - 1) * limit;

    console.log("[DEBUG] Filter:", filter);

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

    console.log("[DEBUG] Found orders:", orders.length, "Total:", total);

    errorLogger.info("Admin fetched orders for date range", {
      userId: session.user.id,
      dateRange: { startDate, endDate },
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
    console.error("[DEBUG] Error:", error);
    errorLogger.error(
      "Error fetching admin orders",
      {},
      error instanceof Error ? error : new Error(message),
    );
    return Response.json({ error: message }, { status: 500 });
  }
}
