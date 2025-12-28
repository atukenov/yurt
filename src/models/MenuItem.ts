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

// Index for faster searches
menuItemSchema.index({ name: "text", category: 1 });

export const MenuItem =
  mongoose.models.MenuItem || mongoose.model("MenuItem", menuItemSchema);
