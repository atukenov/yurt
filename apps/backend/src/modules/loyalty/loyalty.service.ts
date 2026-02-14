import {
  BadRequestException,
  Injectable,
  NotFoundException,
} from "@nestjs/common";
import { InjectModel } from "@nestjs/mongoose";
import { Model } from "mongoose";
import { Loyalty, LoyaltyDocument } from "../../database/schemas";

@Injectable()
export class LoyaltyService {
  constructor(
    @InjectModel(Loyalty.name) private loyaltyModel: Model<LoyaltyDocument>
  ) {}

  async getStatus(userId: string) {
    let loyalty = await this.loyaltyModel
      .findOne({ user: userId })
      .populate("user", "name email");
    if (!loyalty) {
      loyalty = await this.loyaltyModel.create({ user: userId });
    }

    return {
      success: true,
      data: {
        totalPoints: loyalty.totalPoints,
        availablePoints: loyalty.availablePoints,
        tier: loyalty.tier,
        totalSpent: loyalty.totalSpent,
        orderCount: loyalty.orderCount,
        lastOrderDate: loyalty.lastOrderDate,
        birthday: loyalty.birthday,
        tierBenefits: (loyalty as any).getTierBenefits?.(),
      },
    };
  }

  async awardPoints(userId: string, orderId: string, orderAmount: number) {
    if (!orderId || typeof orderAmount !== "number" || orderAmount <= 0) {
      throw new BadRequestException("Invalid orderId or orderAmount");
    }

    let loyalty = await this.loyaltyModel.findOne({ user: userId });
    if (!loyalty) {
      loyalty = await this.loyaltyModel.create({ user: userId });
    }

    const pointsEarned = (loyalty as any).awardPoints(orderAmount, orderId);
    await loyalty.save();

    return {
      success: true,
      data: {
        pointsEarned,
        totalPoints: loyalty.totalPoints,
        availablePoints: loyalty.availablePoints,
        tier: loyalty.tier,
        tierBenefits: (loyalty as any).getTierBenefits?.(),
      },
    };
  }

  async redeemPoints(userId: string, points: number, orderId: string) {
    if (!points || !orderId || points <= 0 || !Number.isInteger(points)) {
      throw new BadRequestException("Invalid points or orderId");
    }

    const loyalty = await this.loyaltyModel.findOne({ user: userId });
    if (!loyalty) throw new NotFoundException("Loyalty account not found");

    if (points > loyalty.availablePoints) {
      throw new BadRequestException({
        error: "Insufficient points",
        availablePoints: loyalty.availablePoints,
      });
    }

    const discount = (loyalty as any).redeemPoints(points, orderId);
    await loyalty.save();

    return {
      success: true,
      data: {
        discount,
        pointsRedeemed: points,
        remainingPoints: loyalty.availablePoints,
      },
    };
  }
}
