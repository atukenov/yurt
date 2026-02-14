import { Injectable, Logger, NotFoundException } from "@nestjs/common";
import { InjectModel } from "@nestjs/mongoose";
import { Model } from "mongoose";
import {
  Location,
  LocationDocument,
  Loyalty,
  LoyaltyDocument,
  MenuItem,
  MenuItemDocument,
  Notification,
  NotificationDocument,
  Order,
  OrderDocument,
  Topping,
  ToppingDocument,
  User,
  UserDocument,
} from "../../database/schemas";
import { OrdersGateway } from "../../websocket/orders.gateway";

@Injectable()
export class AdminService {
  private readonly logger = new Logger(AdminService.name);

  constructor(
    @InjectModel(Order.name) private orderModel: Model<OrderDocument>,
    @InjectModel(MenuItem.name) private menuItemModel: Model<MenuItemDocument>,
    @InjectModel(Topping.name) private toppingModel: Model<ToppingDocument>,
    @InjectModel(Location.name) private locationModel: Model<LocationDocument>,
    @InjectModel(Notification.name)
    private notificationModel: Model<NotificationDocument>,
    @InjectModel(Loyalty.name) private loyaltyModel: Model<LoyaltyDocument>,
    @InjectModel(User.name) private userModel: Model<UserDocument>,
    private readonly ordersGateway: OrdersGateway
  ) {}

  // ─── Orders ──────────────────────────
  async getOrders(query: {
    startDate?: string;
    endDate?: string;
    sortBy?: string;
    sortOrder?: string;
    page?: number;
    limit?: number;
  }) {
    const filter: any = {};
    if (query.startDate || query.endDate) {
      filter.createdAt = {};
      if (query.startDate)
        filter.createdAt.$gte = new Date(query.startDate + "T00:00:00Z");
      if (query.endDate)
        filter.createdAt.$lte = new Date(query.endDate + "T23:59:59Z");
    }

    const sortBy = query.sortBy || "createdAt";
    const sortOrder = query.sortOrder === "asc" ? 1 : -1;
    const page = query.page || 1;
    const limit = query.limit || 20;
    const skip = (page - 1) * limit;

    const orders = await this.orderModel
      .find(filter)
      .populate("customer", "name email phone")
      .populate("location", "name address city")
      .populate("items.menuItem", "name basePrice")
      .populate("items.toppings", "name price")
      .sort({ [sortBy]: sortOrder })
      .skip(skip)
      .limit(limit)
      .lean();

    const total = await this.orderModel.countDocuments(filter);

    return {
      orders,
      pagination: { total, page, limit, pages: Math.ceil(total / limit) },
    };
  }

  async updateOrder(
    orderId: string,
    data: {
      status?: string;
      estimatedPrepTime?: number;
      rejectionReason?: string;
      rejectionComment?: string;
    }
  ) {
    const order = await this.orderModel
      .findByIdAndUpdate(orderId, data, { new: true })
      .populate("customer");

    if (!order) throw new NotFoundException("Order not found");

    const { status } = data;
    const customerId =
      typeof order.customer === "object" && order.customer
        ? (order.customer as any)._id
        : order.customer;

    if (status === "accepted" && customerId) {
      await this.notificationModel.create({
        order: order._id,
        recipient: customerId,
        type: "order_accepted",
        message: `Your order ${order.orderNumber} has been accepted. Estimated time: ${data.estimatedPrepTime} minutes`,
      });
    } else if (status === "rejected" && customerId) {
      await this.notificationModel.create({
        order: order._id,
        recipient: customerId,
        type: "order_rejected",
        message: `Your order ${order.orderNumber} has been rejected. Reason: ${data.rejectionReason}`,
      });
    } else if (status === "completed" && customerId) {
      await this.notificationModel.create({
        order: order._id,
        recipient: customerId,
        type: "order_completed",
        message: `Your order ${order.orderNumber} is ready for pickup!`,
      });
    }

    // Socket events
    const populatedOrder = await this.orderModel.findById(order._id).populate([
      { path: "customer", select: "name email phone" },
      { path: "location", select: "name address city" },
      { path: "items.menuItem", select: "name basePrice" },
    ]);

    if (populatedOrder) {
      this.ordersGateway.emitOrderUpdated(orderId, populatedOrder.toObject());
      if (status) {
        this.ordersGateway.emitOrderStatusChanged(
          orderId,
          status,
          customerId?.toString() || ""
        );
      }
    }

    return { order };
  }

  // ─── Menu Items ──────────────────────
  async getMenuItems() {
    const items = await this.menuItemModel.find();
    return { items };
  }

  async createMenuItem(data: any) {
    const item = await this.menuItemModel.create(data);
    return { message: "Menu item created", item };
  }

  async updateMenuItem(id: string, data: any) {
    const item = await this.menuItemModel.findByIdAndUpdate(id, data, {
      new: true,
    });
    if (!item) throw new NotFoundException("Menu item not found");
    return { item };
  }

  async deleteMenuItem(id: string) {
    await this.menuItemModel.findByIdAndDelete(id);
    return { message: "Menu item deleted" };
  }

  // ─── Toppings ────────────────────────
  async getToppings() {
    const toppings = await this.toppingModel.find();
    return { toppings };
  }

  async createTopping(data: any) {
    const topping = new this.toppingModel(data);
    await topping.save();
    return { topping };
  }

  async updateTopping(id: string, data: any) {
    const topping = await this.toppingModel.findByIdAndUpdate(id, data, {
      new: true,
    });
    if (!topping) throw new NotFoundException("Topping not found");
    return { topping };
  }

  async deleteTopping(id: string) {
    const topping = await this.toppingModel.findByIdAndDelete(id);
    if (!topping) throw new NotFoundException("Topping not found");
    return { message: "Topping deleted successfully" };
  }

  // ─── Locations ───────────────────────
  async getLocations() {
    const locations = await this.locationModel.find();
    return { locations };
  }

  async createLocation(data: any) {
    const location = await this.locationModel.create(data);
    return { message: "Location created", location };
  }

  async updateLocation(id: string, data: any) {
    const location = await this.locationModel.findByIdAndUpdate(id, data, {
      new: true,
    });
    if (!location) throw new NotFoundException("Location not found");
    return { location };
  }

  async deleteLocation(id: string) {
    await this.locationModel.findByIdAndDelete(id);
    return { message: "Location deleted" };
  }

  async updateLocationHours(
    id: string,
    data: { workingHours?: any; holidays?: any }
  ) {
    const update: any = {};
    if (data.workingHours) update.workingHours = data.workingHours;
    if (data.holidays) update.holidays = data.holidays;

    const location = await this.locationModel.findByIdAndUpdate(id, update, {
      new: true,
    });
    if (!location) throw new NotFoundException("Location not found");
    return { message: "Location hours updated successfully", location };
  }

  // ─── Analytics ───────────────────────
  async getAnalytics(timeRange: string) {
    const now = new Date();
    const startDate = new Date();
    if (timeRange === "7days") startDate.setDate(now.getDate() - 7);
    else if (timeRange === "90days") startDate.setDate(now.getDate() - 90);
    else startDate.setDate(now.getDate() - 30);

    const orders = await this.orderModel
      .find({ createdAt: { $gte: startDate, $lte: now } })
      .populate("items.menuItem")
      .populate("customer")
      .populate("location");

    const totalOrders = orders.length;
    const totalRevenue = orders.reduce((sum, o) => sum + o.totalPrice, 0);
    const completedOrders = orders.filter(
      (o) => o.status === "completed"
    ).length;
    const pendingOrders = orders.filter(
      (o) => o.status === "pending" || o.status === "accepted"
    ).length;
    const averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;

    // Popular menu items
    const menuItemsMap: Record<
      string,
      { name: string; count: number; revenue: number }
    > = {};
    orders.forEach((order) => {
      order.items.forEach((item: any) => {
        const menuItem = item.menuItem as any;
        const itemId = menuItem?._id?.toString() || "unknown";
        const itemName = menuItem?.name || "Unknown Item";
        const quantity = item.quantity || 1;
        const priceAtOrder = item.priceAtOrder || 0;
        if (!menuItemsMap[itemId])
          menuItemsMap[itemId] = { name: itemName, count: 0, revenue: 0 };
        menuItemsMap[itemId].count += quantity;
        menuItemsMap[itemId].revenue += priceAtOrder * quantity;
      });
    });
    const populateMenuItems = Object.values(menuItemsMap)
      .sort((a, b) => b.count - a.count)
      .slice(0, 10);

    // Revenue by day
    const revenueByDayMap: Record<string, { revenue: number; orders: number }> =
      {};
    orders.forEach((order) => {
      const dateKey = new Date((order as any).createdAt)
        .toISOString()
        .split("T")[0];
      if (!revenueByDayMap[dateKey])
        revenueByDayMap[dateKey] = { revenue: 0, orders: 0 };
      revenueByDayMap[dateKey].revenue += order.totalPrice;
      revenueByDayMap[dateKey].orders += 1;
    });
    const revenueByDay = Object.entries(revenueByDayMap)
      .map(([date, data]) => ({ date, ...data }))
      .sort((a, b) => a.date.localeCompare(b.date));

    // Peak hours
    const peakHoursMap: Record<number, number> = {};
    for (let i = 0; i < 24; i++) peakHoursMap[i] = 0;
    orders.forEach((order) => {
      peakHoursMap[new Date((order as any).createdAt).getHours()]++;
    });
    const peakHours = Object.entries(peakHoursMap).map(([hour, count]) => ({
      hour: parseInt(hour),
      count,
    }));

    // Payment method breakdown
    const paymentMethodBreakdown = {
      kaspi: orders.filter((o) => o.paymentMethod === "kaspi").length,
      applepay: orders.filter((o) => o.paymentMethod === "applepay").length,
    };

    // Top customers
    const customersMap: Record<
      string,
      { name: string; email: string; orderCount: number; totalSpent: number }
    > = {};
    orders.forEach((order) => {
      const cust = order.customer as any;
      const customerId = cust?._id?.toString() || "unknown";
      if (!customersMap[customerId]) {
        customersMap[customerId] = {
          name: cust?.name || "Unknown",
          email: cust?.email || "unknown@example.com",
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

    return {
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
    };
  }

  // ─── Loyalty Analytics ───────────────
  async getLoyaltyAnalytics(timeRange: string) {
    const now = new Date();
    const daysAgo = timeRange === "7d" ? 7 : timeRange === "90d" ? 90 : 30;
    const startDate = new Date(now.getTime() - daysAgo * 24 * 60 * 60 * 1000);

    const loyalties = await this.loyaltyModel
      .find()
      .populate("user", "name email");

    const totalMembers = loyalties.length;
    const totalPointsIssued = loyalties.reduce(
      (sum, l) => sum + (l.totalPoints || 0),
      0
    );
    const totalPointsRedeemed = loyalties.reduce(
      (sum, l) =>
        sum +
        ((l.redemptionHistory as any[])?.reduce(
          (s: number, r: any) => s + (r.points || 0),
          0
        ) || 0),
      0
    );
    const totalDiscountsGiven = Math.floor(totalPointsRedeemed / 100);
    const avgPointsPerCustomer =
      totalMembers > 0 ? Math.round(totalPointsIssued / totalMembers) : 0;
    const avgSpendPerCustomer =
      totalMembers > 0
        ? parseFloat(
            (
              loyalties.reduce((sum, l) => sum + (l.totalSpent || 0), 0) /
              totalMembers
            ).toFixed(2)
          )
        : 0;

    const tierCounts: Record<string, number> = {
      bronze: 0,
      silver: 0,
      gold: 0,
      platinum: 0,
    };
    const tierStats: Record<string, { points: number; count: number }> = {
      bronze: { points: 0, count: 0 },
      silver: { points: 0, count: 0 },
      gold: { points: 0, count: 0 },
      platinum: { points: 0, count: 0 },
    };
    loyalties.forEach((l) => {
      const tier = l.tier || "bronze";
      tierCounts[tier]++;
      tierStats[tier].points += l.totalPoints || 0;
      tierStats[tier].count++;
    });
    const tierDistribution = Object.keys(tierCounts).map((tier) => ({
      tier,
      count: tierCounts[tier],
      percentage:
        totalMembers > 0
          ? Math.round((tierCounts[tier] / totalMembers) * 100)
          : 0,
      totalPoints: tierStats[tier].points,
      avgPoints:
        tierStats[tier].count > 0
          ? Math.round(tierStats[tier].points / tierStats[tier].count)
          : 0,
    }));

    const topCustomers = loyalties
      .sort((a, b) => (b.totalSpent || 0) - (a.totalSpent || 0))
      .slice(0, 10)
      .map((l) => ({
        userId: (l.user as any)._id.toString(),
        name: (l.user as any).name || "Unknown",
        tier: l.tier || "bronze",
        totalPoints: l.totalPoints || 0,
        totalSpent: l.totalSpent || 0,
        orderCount: l.orderCount || 0,
        pointsRedeemed:
          (l.redemptionHistory as any[])?.reduce(
            (s: number, r: any) => s + (r.points || 0),
            0
          ) || 0,
      }));

    const redemptionTrends: Record<
      string,
      { count: number; points: number; discount: number }
    > = {};
    loyalties.forEach((l) => {
      (l.redemptionHistory as any[])?.forEach((r: any) => {
        if (r.redeemedAt && new Date(r.redeemedAt) >= startDate) {
          const date = new Date(r.redeemedAt).toISOString().split("T")[0];
          if (!redemptionTrends[date])
            redemptionTrends[date] = { count: 0, points: 0, discount: 0 };
          redemptionTrends[date].count++;
          redemptionTrends[date].points += r.points || 0;
          redemptionTrends[date].discount += r.discount || 0;
        }
      });
    });
    const redemptionTrendArray = Object.entries(redemptionTrends)
      .sort(([a], [b]) => b.localeCompare(a))
      .slice(0, 14)
      .map(([date, data]) => ({
        date,
        count: data.count,
        totalPointsRedeemed: data.points,
        totalDiscountGiven: data.discount,
      }));

    const currentMonth = now.getMonth();
    const birthdayBonusesAwarded = loyalties.filter((l) => {
      if (!l.birthday) return false;
      return (
        new Date(l.birthday).getMonth() === currentMonth && !l.birthdayBonusUsed
      );
    }).length;

    return {
      totalMembers,
      totalPointsIssued,
      totalPointsRedeemed,
      totalDiscountsGiven,
      avgPointsPerCustomer,
      avgSpendPerCustomer,
      tierDistribution,
      topCustomers,
      redemptionTrends: redemptionTrendArray,
      birthdayBonusesAwarded,
    };
  }
}
