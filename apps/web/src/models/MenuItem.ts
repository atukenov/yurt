import mongoose from "mongoose";

const menuItemSchema = new mongoose.Schema(
  {
    name: {
      type: String,
      required: true,
    },
    description: {
      type: String,
    },
    category: {
      type: String,
      enum: ["hot", "cold", "latte", "cappuccino", "espresso", "specialty"],
      required: true,
    },
    basePrice: {
      type: Number,
      required: true,
    },
    sizes: [
      {
        size: {
          type: String,
          enum: ["small", "medium", "large"],
        },
        priceModifier: Number, // Added to basePrice
      },
    ],
    image: {
      type: String,
    },
    isAvailable: {
      type: Boolean,
      default: true,
    },
    preparationTime: {
      type: Number, // in minutes
      default: 5,
    },
  },
  { timestamps: true }
);

// Indexes for faster searches and queries
menuItemSchema.index({ name: "text", category: 1 });
menuItemSchema.index({ category: 1, isAvailable: 1 });
menuItemSchema.index({ isAvailable: 1, createdAt: -1 });
menuItemSchema.index({ basePrice: 1 });
menuItemSchema.index({ category: 1, basePrice: 1 });

export const MenuItem =
  mongoose.models.MenuItem || mongoose.model("MenuItem", menuItemSchema);
