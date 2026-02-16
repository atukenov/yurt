import {
  BadRequestException,
  ForbiddenException,
  Injectable,
  Logger,
  NotFoundException,
} from "@nestjs/common";
import { InjectModel } from "@nestjs/mongoose";
import { Model } from "mongoose";
import {
  Location,
  LocationDocument,
  Loyalty,
  LoyaltyDocument,
  Notification,
  NotificationDocument,
  Order,
  OrderDocument,
  User,
  UserDocument,
} from "../../database/schemas";
import { OrdersGateway } from "../../websocket/orders.gateway";

// Inline location availability check (same logic as lib/locationAvailability.ts)
function isLocationOpen(location: any): { isOpen: boolean; reason?: string } {
  const now = new Date();
  const dayOfWeek = now.getDay();
  const dayIndex = dayOfWeek === 0 ? 6 : dayOfWeek - 1;
  const days = [
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
  ];
  const currentDay = days[dayIndex];

  if (!location.isActive) {
    return { isOpen: false, reason: "Location is currently inactive" };
  }

  // Check holidays
  const today = new Date();
  today.setHours(0, 0, 0, 0);

  for (const holiday of location.holidays || []) {
    const holidayDate = new Date(holiday.date);
    holidayDate.setHours(0, 0, 0, 0);
    if (holidayDate.getTime() === today.getTime()) {
      if (holiday.isClosed) {
        return {
          isOpen: false,
          reason: `Closed for ${holiday.name || "holiday"}`,
        };
      }
      if (holiday.customHours?.open && holiday.customHours?.close) {
        const timeString = formatTime(now);
        const isOpen =
          timeString >= holiday.customHours.open &&
          timeString < holiday.customHours.close;
        return {
          isOpen,
          reason: isOpen
            ? undefined
            : `Closed (${holiday.name || "Holiday hours"})`,
        };
      }
    }
  }

  const hours = location.workingHours?.[currentDay];
  if (!hours) {
    return { isOpen: false, reason: "Location information not available" };
  }

  const timeString = formatTime(now);
  const isOpen = timeString >= hours.open && timeString < hours.close;
  return {
    isOpen,
    reason: !isOpen ? `Location closed. Opens at ${hours.open}` : undefined,
  };
}

function formatTime(date: Date): string {
  const hours = String(date.getHours()).padStart(2, "0");
  const minutes = String(date.getMinutes()).padStart(2, "0");
  return `${hours}:${minutes}`;
}

@Injectable()
export class OrdersService {
  private readonly logger = new Logger(OrdersService.name);

  constructor(
    @InjectModel(Order.name) private orderModel: Model<OrderDocument>,
    @InjectModel(Location.name) private locationModel: Model<LocationDocument>,
    @InjectModel(User.name) private userModel: Model<UserDocument>,
    @InjectModel(Notification.name)
    private notificationModel: Model<NotificationDocument>,
    @InjectModel(Loyalty.name) private loyaltyModel: Model<LoyaltyDocument>,
    private readonly ordersGateway: OrdersGateway
  ) {}

  async createOrder(
    userId: string,
    data: {
      locationId: string;
      items: Array<{
        menuItemId: string;
        quantity: number;
        size: string;
        toppings?: string[];
        specialInstructions?: string;
      }>;
      totalPrice: number;
      paymentMethod: string;
      notes?: string;
    }
  ) {
    const { locationId, items, totalPrice, paymentMethod, notes } = data;

    // Check location
    const location = await this.locationModel.findById(locationId);
    if (!location) throw new NotFoundException("Location not found");

    const availability = isLocationOpen(location);
    if (!availability.isOpen) {
      throw new BadRequestException({
        error: availability.reason || "Location is currently closed",
        isLocationClosed: true,
      });
    }

    // Generate order number
    const orderNumber = `ORD-${Date.now()}-${Math.random().toString(36).substr(2, 5).toUpperCase()}`;

    // Create order
    const order = await this.orderModel.create({
      orderNumber,
      customer: userId,
      location: locationId,
      items: items.map((item) => ({
        menuItem: item.menuItemId,
        quantity: item.quantity,
        size: item.size,
        toppings: item.toppings || [],
        specialInstructions: item.specialInstructions,
        priceAtOrder: totalPrice / items.length,
      })),
      totalPrice,
      paymentMethod,
      notes,
      paymentStatus: "pending",
      status: "pending",
    });

    // Notify admins
    const adminUsers = await this.userModel.find({ role: "admin" });
    for (const admin of adminUsers) {
      await this.notificationModel.create({
        order: order._id,
        recipient: admin._id,
        type: "order_accepted",
        message: `New order ${orderNumber} received`,
      });
    }

    // Emit socket event
    const populatedOrder = await order.populate([
      { path: "customer", select: "name email phone" },
      { path: "location", select: "name address city" },
      { path: "items.menuItem", select: "name basePrice" },
    ]);

    this.ordersGateway.emitOrderCreated({
      ...populatedOrder.toObject(),
      userId,
    });

    // Award loyalty points
    try {
      let loyaltyRecord = await this.loyaltyModel.findOne({ user: userId });
      if (!loyaltyRecord) {
        loyaltyRecord = new this.loyaltyModel({ user: userId });
      }

      const pointsEarned = Math.floor(
        totalPrice *
          ((loyaltyRecord as any).getTierBenefits?.()?.multiplier || 1)
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

      (loyaltyRecord as any).calculateTier?.();
      await loyaltyRecord.save();
    } catch (loyaltyError) {
      this.logger.warn("Error awarding loyalty points", loyaltyError);
    }

    return {
      message: "Order created successfully",
      order: {
        id: order._id,
        orderNumber: order.orderNumber,
        status: order.status,
      },
    };
  }

  async getCustomerOrders(userId: string) {
    const orders = await this.orderModel
      .find({ customer: userId })
      .populate("location", "name address")
      .populate("items.menuItem", "name")
      .sort({ createdAt: -1 })
      .limit(20);

    return { orders };
  }

  async getOrderById(orderId: string, userId: string, userRole: string) {
    const order = await this.orderModel
      .findById(orderId)
      .populate("customer", "name email phone")
      .populate("location", "name address")
      .populate("items.menuItem", "name")
      .populate("items.toppings", "name price");

    if (!order) throw new NotFoundException("Order not found");

    if (order.customer._id.toString() !== userId && userRole !== "admin") {
      throw new ForbiddenException("Unauthorized");
    }

    return { order };
  }
}
