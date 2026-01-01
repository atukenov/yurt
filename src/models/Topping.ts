import mongoose from "mongoose";

const toppingSchema = new mongoose.Schema(
  {
    name: {
      type: String,
      required: true,
      index: true,
    },
    price: {
      type: Number,
      required: true,
      default: 0,
    },
    category: {
      type: String,
      enum: ["syrup", "shot", "milk", "topping"],
      default: "topping",
      index: true,
    },
    description: {
      type: String,
    },
  },
  { timestamps: true }
);

// Compound index for category browsing
toppingSchema.index({ category: 1, name: 1 });

export const Topping =
  mongoose.models.Topping || mongoose.model("Topping", toppingSchema);
