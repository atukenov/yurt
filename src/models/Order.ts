import mongoose from "mongoose";

const orderItemSchema = new mongoose.Schema({
  menuItem: {
    type: mongoose.Schema.Types.ObjectId,
    ref: "MenuItem",
    required: true,
  },
  quantity: {
    type: Number,
    required: true,
    min: 1,
  },
  size: {
    type: String,
    enum: ["small", "medium", "large"],
    required: true,
  },
  toppings: [
    {
      type: mongoose.Schema.Types.ObjectId,
      ref: "Topping",
    },
  ],
  specialInstructions: String,
  priceAtOrder: {
    type: Number,
    required: true,
  },
});

const orderSchema = new mongoose.Schema(
  {
    orderNumber: {
      type: String,
      unique: true,
      required: true,
    },
    customer: {
      type: mongoose.Schema.Types.ObjectId,
      ref: "User",
      required: true,
    },
    location: {
      type: mongoose.Schema.Types.ObjectId,
      ref: "Location",
      required: true,
    },
    items: [orderItemSchema],
    status: {
      type: String,
      enum: ["pending", "accepted", "rejected", "completed", "cancelled"],
      default: "pending",
    },
    totalPrice: {
      type: Number,
      required: true,
    },
    estimatedPrepTime: {
      type: Number, // in minutes
    },
    rejectionReason: {
      type: String,
      enum: [
        "no_milk",
        "no_coffee_beans",
        "size_unavailable",
        "equipment_issue",
        "custom",
      ],
    },
    rejectionComment: String,
    paymentStatus: {
      type: String,
      enum: ["pending", "completed", "failed"],
      default: "pending",
    },
    paymentMethod: {
      type: String,
      enum: ["cash", "card", "stripe"],
      default: "cash",
    },
    notes: String,
  },
  { timestamps: true }
);

// Index for faster queries
orderSchema.index({ customer: 1, createdAt: -1 });
orderSchema.index({ location: 1, status: 1, createdAt: -1 });

export const Order =
  mongoose.models.Order || mongoose.model("Order", orderSchema);
