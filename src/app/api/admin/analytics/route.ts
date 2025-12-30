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
    const timeRange = searchParams.get("timeRange") || "30days";

    // Calculate date range
    const now = new Date();
    const startDate = new Date();

    if (timeRange === "7days") {
      startDate.setDate(now.getDate() - 7);
    } else if (timeRange === "30days") {
      startDate.setDate(now.getDate() - 30);
    } else if (timeRange === "90days") {
      startDate.setDate(now.getDate() - 90);
    }

    // Fetch all orders in the time range
    const orders = await Order.find({
      createdAt: { $gte: startDate, $lte: now },
    })
      .populate("items.menuItem")
      .populate("customer")
      .populate("location");

    // Calculate metrics
    const totalOrders = orders.length;
    const totalRevenue = orders.reduce(
      (sum, order) => sum + order.totalPrice,
      0
    );
    const completedOrders = orders.filter(
      (order) => order.status === "completed"
    ).length;
    const pendingOrders = orders.filter(
      (order) => order.status === "pending" || order.status === "accepted"
    ).length;
    const averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

    // Popular menu items
    const menuItemsMap: {
      [key: string]: { name: string; count: number; revenue: number };
    } = {};
    orders.forEach((order) => {
      order.items.forEach((item: Record<string, unknown>) => {
        const menuItem = item.menuItem as
          | { _id?: string; name?: string }
          | undefined;
        const itemId = menuItem?._id?.toString() || "unknown";
        const itemName = menuItem?.name || "Unknown Item";
        const quantity = (item.quantity as number) || 1;
        const priceAtOrder = (item.priceAtOrder as number) || 0;

        if (!menuItemsMap[itemId]) {
          menuItemsMap[itemId] = {
            name: itemName,
            count: 0,
            revenue: 0,
          };
        }

        menuItemsMap[itemId].count += quantity;
        menuItemsMap[itemId].revenue += priceAtOrder * quantity;
      });
    });

    const populateMenuItems = Object.values(menuItemsMap)
      .sort((a, b) => b.count - a.count)
      .slice(0, 10);

    // Revenue by day
    const revenueByDayMap: {
      [key: string]: { revenue: number; orders: number };
    } = {};
    orders.forEach((order) => {
      const dateKey = new Date(order.createdAt).toISOString().split("T")[0];
      if (!revenueByDayMap[dateKey]) {
        revenueByDayMap[dateKey] = { revenue: 0, orders: 0 };
      }
      revenueByDayMap[dateKey].revenue += order.totalPrice;
      revenueByDayMap[dateKey].orders += 1;
    });

    const revenueByDay = Object.entries(revenueByDayMap)
      .map(([date, data]) => ({
        date,
        ...data,
      }))
      .sort((a, b) => a.date.localeCompare(b.date));

    // Peak order hours
    const peakHoursMap: { [key: number]: number } = {};
    for (let i = 0; i < 24; i++) {
      peakHoursMap[i] = 0;
    }

    orders.forEach((order) => {
      const hour = new Date(order.createdAt).getHours();
      peakHoursMap[hour] = (peakHoursMap[hour] || 0) + 1;
    });

    const peakHours = Object.entries(peakHoursMap).map(([hour, count]) => ({
      hour: parseInt(hour),
      count,
    }));

    // Payment method breakdown
    const paymentMethodBreakdown = {
      cash: orders.filter((o) => o.paymentMethod === "cash").length,
      card: orders.filter((o) => o.paymentMethod === "card").length,
      stripe: orders.filter((o) => o.paymentMethod === "stripe").length,
    };

    // Top customers
    const customersMap: {
      [key: string]: {
        name: string;
        email: string;
        orderCount: number;
        totalSpent: number;
      };
    } = {};

    orders.forEach((order) => {
      const customerId = order.customer?._id?.toString() || "unknown";
      const customerName = order.customer?.name || "Unknown";
      const customerEmail = order.customer?.email || "unknown@example.com";

      if (!customersMap[customerId]) {
        customersMap[customerId] = {
          name: customerName,
          email: customerEmail,
          orderCount: 0,
          totalSpent: 0,
        };
      }

      customersMap[customerId].orderCount += 1;
      customersMap[customerId].totalSpent += order.totalPrice;
    });

    const topCustomers = Object.values(customersMap)
      .sort((a, b) => b.totalSpent - a.totalSpent)
      .slice(0, 20);

    return Response.json({
      totalOrders,
      totalRevenue,
      completedOrders,
      pendingOrders,
      averageOrderValue,
      populateMenuItems,
      revenueByDay,
      peakHours,
      paymentMethodBreakdown,
      topCustomers,
    });
  } catch (error) {
    console.error("Error fetching analytics:", error);
    return Response.json(
      { error: "Failed to fetch analytics" },
      { status: 500 }
    );
  }
}
