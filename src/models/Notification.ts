import mongoose from "mongoose";

const notificationSchema = new mongoose.Schema(
  {
    order: {
      type: mongoose.Schema.Types.ObjectId,
      ref: "Order",
      required: true,
    },
    recipient: {
      type: mongoose.Schema.Types.ObjectId,
      ref: "User",
      required: true,
    },
    type: {
      type: String,
      enum: ["order_accepted", "order_rejected", "order_completed"],
      required: true,
    },
    message: String,
    read: {
      type: Boolean,
      default: false,
    },
  },
  { timestamps: true }
);

// Indexes for faster queries
notificationSchema.index({ recipient: 1, createdAt: -1 });
notificationSchema.index({ recipient: 1, read: 1 });
notificationSchema.index({ read: 1, createdAt: -1 });
notificationSchema.index({ order: 1 });

export const Notification =
  (mongoose.models.Notification as mongoose.Model<any>) ||
  mongoose.model("Notification", notificationSchema);
