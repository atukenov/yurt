import mongoose from "mongoose";

const locationSchema = new mongoose.Schema(
  {
    name: {
      type: String,
      required: true,
    },
    address: {
      type: String,
      required: true,
    },
    city: {
      type: String,
      required: true,
    },
    zipCode: {
      type: String,
      required: true,
    },
    phone: {
      type: String,
    },
    latitude: {
      type: Number,
    },
    longitude: {
      type: Number,
    },
    workingHours: {
      monday: {
        open: { type: String, default: "06:00" },
        close: { type: String, default: "20:00" },
      },
      tuesday: {
        open: { type: String, default: "06:00" },
        close: { type: String, default: "20:00" },
      },
      wednesday: {
        open: { type: String, default: "06:00" },
        close: { type: String, default: "20:00" },
      },
      thursday: {
        open: { type: String, default: "06:00" },
        close: { type: String, default: "20:00" },
      },
      friday: {
        open: { type: String, default: "06:00" },
        close: { type: String, default: "20:00" },
      },
      saturday: {
        open: { type: String, default: "07:00" },
        close: { type: String, default: "21:00" },
      },
      sunday: {
        open: { type: String, default: "07:00" },
        close: { type: String, default: "21:00" },
      },
    },
    isActive: {
      type: Boolean,
      default: true,
    },
    availableMenuItems: [
      {
        type: mongoose.Schema.Types.ObjectId,
        ref: "MenuItem",
      },
    ],
  },
  { timestamps: true }
);

export const Location =
  mongoose.models.Location || mongoose.model("Location", locationSchema);
