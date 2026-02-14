import { Prop, Schema, SchemaFactory, raw } from "@nestjs/mongoose";
import { HydratedDocument, Types } from "mongoose";

// Tier definitions (same as original)
export enum LoyaltyTier {
  BRONZE = "bronze",
  SILVER = "silver",
  GOLD = "gold",
  PLATINUM = "platinum",
}

export const TIER_THRESHOLDS = {
  [LoyaltyTier.BRONZE]: 0,
  [LoyaltyTier.SILVER]: 500,
  [LoyaltyTier.GOLD]: 1500,
  [LoyaltyTier.PLATINUM]: 3000,
};

export const TIER_MULTIPLIERS = {
  [LoyaltyTier.BRONZE]: 1.0,
  [LoyaltyTier.SILVER]: 1.25,
  [LoyaltyTier.GOLD]: 1.5,
  [LoyaltyTier.PLATINUM]: 2.0,
};

export const REDEMPTION_RATE = 100; // 100 points = $1 discount

export type LoyaltyDocument = HydratedDocument<Loyalty>;

@Schema({ timestamps: true })
export class Loyalty {
  @Prop({
    type: Types.ObjectId,
    ref: "User",
    required: true,
    unique: true,
    index: true,
  })
  user: Types.ObjectId;

  @Prop({ default: 0, min: 0 })
  totalPoints: number;

  @Prop({ default: 0, min: 0 })
  availablePoints: number;

  @Prop({ enum: Object.values(LoyaltyTier), default: LoyaltyTier.BRONZE })
  tier: string;

  @Prop({ default: 0, min: 0 })
  totalSpent: number;

  @Prop({ default: 0, min: 0 })
  orderCount: number;

  @Prop()
  lastOrderDate: Date;

  @Prop()
  birthday: Date;

  @Prop({ default: false })
  birthdayBonusUsed: boolean;

  @Prop([
    raw({
      points: { type: Number },
      discount: { type: Number },
      orderId: { type: Types.ObjectId, ref: "Order" },
      redeemedAt: { type: Date },
    }),
  ])
  redemptionHistory: Record<string, any>[];

  @Prop([
    raw({
      type: {
        type: String,
        enum: ["earned", "redeemed", "bonus", "expired", "refunded"],
      },
      points: { type: Number },
      orderId: { type: Types.ObjectId, ref: "Order" },
      description: { type: String },
      createdAt: { type: Date, default: Date.now },
    }),
  ])
  pointsHistory: Record<string, any>[];
}

export const LoyaltySchema = SchemaFactory.createForClass(Loyalty);

// Indexes for common queries
LoyaltySchema.index({ user: 1, tier: 1 });
LoyaltySchema.index({ totalPoints: -1 });
LoyaltySchema.index({ totalSpent: -1 });
LoyaltySchema.index({ "pointsHistory.createdAt": -1 });

// Calculate tier based on total points
LoyaltySchema.methods.calculateTier = function () {
  let newTier = LoyaltyTier.BRONZE;

  if (this.totalPoints >= TIER_THRESHOLDS[LoyaltyTier.PLATINUM]) {
    newTier = LoyaltyTier.PLATINUM;
  } else if (this.totalPoints >= TIER_THRESHOLDS[LoyaltyTier.GOLD]) {
    newTier = LoyaltyTier.GOLD;
  } else if (this.totalPoints >= TIER_THRESHOLDS[LoyaltyTier.SILVER]) {
    newTier = LoyaltyTier.SILVER;
  }

  this.tier = newTier;
  return newTier;
};

// Award points for order
LoyaltySchema.methods.awardPoints = function (amount: number, orderId: string) {
  const multiplier =
    TIER_MULTIPLIERS[this.tier as keyof typeof TIER_MULTIPLIERS] || 1.0;
  const pointsEarned = Math.floor(amount * multiplier);

  this.totalPoints += pointsEarned;
  this.availablePoints += pointsEarned;
  this.totalSpent += amount;
  this.orderCount += 1;
  this.lastOrderDate = new Date();

  this.pointsHistory.push({
    type: "earned",
    points: pointsEarned,
    orderId: new Types.ObjectId(orderId),
    description: `Earned ${pointsEarned} points from order ($${amount})`,
  });

  this.calculateTier();
  return pointsEarned;
};

// Award birthday bonus
LoyaltySchema.methods.awardBirthdayBonus = function () {
  if (!this.birthday) return 0;

  const today = new Date();
  const birthdayThisYear = new Date(
    today.getFullYear(),
    this.birthday.getMonth(),
    this.birthday.getDate()
  );

  const daysUntilBirthday = Math.abs(
    Math.ceil(
      (birthdayThisYear.getTime() - today.getTime()) / (1000 * 60 * 60 * 24)
    )
  );

  if (daysUntilBirthday <= 7 && !this.birthdayBonusUsed) {
    const bonusPoints = 100;
    this.availablePoints += bonusPoints;
    this.totalPoints += bonusPoints;
    this.birthdayBonusUsed = true;

    this.pointsHistory.push({
      type: "bonus",
      points: bonusPoints,
      description: "Birthday bonus",
    });

    return bonusPoints;
  }

  return 0;
};

// Redeem points for discount
LoyaltySchema.methods.redeemPoints = function (
  pointsToRedeem: number,
  orderId: string
) {
  if (pointsToRedeem > this.availablePoints) {
    throw new Error("Insufficient points to redeem");
  }

  const discount = pointsToRedeem / REDEMPTION_RATE;

  this.availablePoints -= pointsToRedeem;
  this.redemptionHistory.push({
    points: pointsToRedeem,
    discount,
    orderId: new Types.ObjectId(orderId),
    redeemedAt: new Date(),
  });

  this.pointsHistory.push({
    type: "redeemed",
    points: pointsToRedeem,
    orderId: new Types.ObjectId(orderId),
    description: `Redeemed ${pointsToRedeem} points for $${discount.toFixed(2)} discount`,
  });

  return discount;
};

// Get tier benefits summary
LoyaltySchema.methods.getTierBenefits = function () {
  const multiplier =
    TIER_MULTIPLIERS[this.tier as keyof typeof TIER_MULTIPLIERS];
  return {
    tier: this.tier,
    pointsPerDollar: multiplier,
    redemptionRate: REDEMPTION_RATE,
    nextTier: this.getNextTier(),
    pointsUntilNextTier: this.getPointsUntilNextTier(),
  };
};

// Get next tier
LoyaltySchema.methods.getNextTier = function () {
  const tiers = [
    LoyaltyTier.BRONZE,
    LoyaltyTier.SILVER,
    LoyaltyTier.GOLD,
    LoyaltyTier.PLATINUM,
  ];
  const currentIndex = tiers.indexOf(this.tier);
  return currentIndex < tiers.length - 1 ? tiers[currentIndex + 1] : null;
};

// Get points until next tier
LoyaltySchema.methods.getPointsUntilNextTier = function () {
  const nextTier = this.getNextTier();
  if (!nextTier) return 0;
  const nextThreshold =
    TIER_THRESHOLDS[nextTier as keyof typeof TIER_THRESHOLDS];
  return Math.max(0, nextThreshold - this.totalPoints);
};
