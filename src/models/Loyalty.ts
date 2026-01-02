import mongoose from "mongoose";

// Tier definitions
export enum LoyaltyTier {
  BRONZE = "bronze",
  SILVER = "silver",
  GOLD = "gold",
  PLATINUM = "platinum",
}

// Tier thresholds (points required to reach each tier)
export const TIER_THRESHOLDS = {
  [LoyaltyTier.BRONZE]: 0,
  [LoyaltyTier.SILVER]: 500,
  [LoyaltyTier.GOLD]: 1500,
  [LoyaltyTier.PLATINUM]: 3000,
};

// Tier benefits (points earned per dollar spent)
export const TIER_MULTIPLIERS = {
  [LoyaltyTier.BRONZE]: 1.0, // 1 point per $1
  [LoyaltyTier.SILVER]: 1.25, // 1.25 points per $1
  [LoyaltyTier.GOLD]: 1.5, // 1.5 points per $1
  [LoyaltyTier.PLATINUM]: 2.0, // 2 points per $1
};

// Redemption rates (discount per point)
export const REDEMPTION_RATE = 100; // 100 points = $1 discount

const loyaltySchema = new mongoose.Schema(
  {
    user: {
      type: mongoose.Schema.Types.ObjectId,
      ref: "User",
      required: true,
      unique: true,
      index: true,
    },
    totalPoints: {
      type: Number,
      default: 0,
      min: 0,
    },
    availablePoints: {
      type: Number,
      default: 0,
      min: 0,
    },
    tier: {
      type: String,
      enum: Object.values(LoyaltyTier),
      default: LoyaltyTier.BRONZE,
    },
    totalSpent: {
      type: Number,
      default: 0,
      min: 0,
    },
    orderCount: {
      type: Number,
      default: 0,
      min: 0,
    },
    lastOrderDate: {
      type: Date,
    },
    birthday: {
      type: Date, // Customer's birthday for birthday bonus
    },
    birthdayBonusUsed: {
      type: Boolean,
      default: false, // Track if birthday bonus was already used this year
    },
    redemptionHistory: [
      {
        points: Number,
        discount: Number,
        orderId: {
          type: mongoose.Schema.Types.ObjectId,
          ref: "Order",
        },
        redeemedAt: Date,
      },
    ],
    pointsHistory: [
      {
        type: {
          type: String,
          enum: ["earned", "redeemed", "bonus", "expired", "refunded"],
        },
        points: Number,
        orderId: {
          type: mongoose.Schema.Types.ObjectId,
          ref: "Order",
        },
        description: String,
        createdAt: {
          type: Date,
          default: Date.now,
        },
      },
    ],
  },
  { timestamps: true }
);

// Indexes for common queries
loyaltySchema.index({ user: 1, tier: 1 });
loyaltySchema.index({ totalPoints: -1 });
loyaltySchema.index({ totalSpent: -1 });
loyaltySchema.index({ "pointsHistory.createdAt": -1 });

// Calculate tier based on total points
loyaltySchema.methods.calculateTier = function () {
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
loyaltySchema.methods.awardPoints = function (amount: number, orderId: string) {
  const multiplier =
    TIER_MULTIPLIERS[this.tier as keyof typeof TIER_MULTIPLIERS] || 1.0;
  const pointsEarned = Math.floor(amount * multiplier);

  this.totalPoints += pointsEarned;
  this.availablePoints += pointsEarned;
  this.totalSpent += amount;
  this.orderCount += 1;
  this.lastOrderDate = new Date();

  // Add to points history
  this.pointsHistory.push({
    type: "earned",
    points: pointsEarned,
    orderId: new mongoose.Types.ObjectId(orderId),
    description: `Earned ${pointsEarned} points from order ($${amount})`,
  });

  // Recalculate tier
  this.calculateTier();

  return pointsEarned;
};

// Award birthday bonus
loyaltySchema.methods.awardBirthdayBonus = function () {
  if (!this.birthday) return 0;

  const today = new Date();
  const birthdayThisYear = new Date(
    today.getFullYear(),
    this.birthday.getMonth(),
    this.birthday.getDate()
  );

  // Check if it's around their birthday (within 7 days) and bonus hasn't been used
  const daysUntilBirthday = Math.abs(
    Math.ceil(
      (birthdayThisYear.getTime() - today.getTime()) / (1000 * 60 * 60 * 24)
    )
  );

  if (daysUntilBirthday <= 7 && !this.birthdayBonusUsed) {
    const bonusPoints = 100; // Birthday bonus
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
loyaltySchema.methods.redeemPoints = function (
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
    orderId: new mongoose.Types.ObjectId(orderId),
    redeemedAt: new Date(),
  });

  this.pointsHistory.push({
    type: "redeemed",
    points: pointsToRedeem,
    orderId: new mongoose.Types.ObjectId(orderId),
    description: `Redeemed ${pointsToRedeem} points for $${discount.toFixed(
      2
    )} discount`,
  });

  return discount;
};

// Get tier benefits summary
loyaltySchema.methods.getTierBenefits = function () {
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
loyaltySchema.methods.getNextTier = function () {
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
loyaltySchema.methods.getPointsUntilNextTier = function () {
  const nextTier = this.getNextTier();
  if (!nextTier) return 0; // Already at top tier
  const nextThreshold =
    TIER_THRESHOLDS[nextTier as keyof typeof TIER_THRESHOLDS];
  return Math.max(0, nextThreshold - this.totalPoints);
};

export const Loyalty =
  mongoose.models.Loyalty || mongoose.model("Loyalty", loyaltySchema);

export type ILoyalty = typeof loyaltySchema;
