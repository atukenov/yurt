import mongoose from "mongoose";

const toppingSchema = new mongoose.Schema(
  {
    name: {
      type: String,
      required: true,
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
    },
    description: {
      type: String,
    },
  },
  { timestamps: true }
);

export const Topping =
  mongoose.models.Topping || mongoose.model("Topping", toppingSchema);
