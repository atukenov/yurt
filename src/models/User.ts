import mongoose from "mongoose";

const userSchema = new mongoose.Schema(
  {
    email: {
      type: String,
      required: true,
      unique: true,
      lowercase: true,
      index: true,
    },
    pinCode: {
      type: String,
      required: true,
      minlength: 4,
      maxlength: 4,
    },
    name: {
      type: String,
      required: true,
      index: true,
    },
    firstName: {
      type: String,
    },
    lastName: {
      type: String,
    },
    phone: {
      type: String,
    },
    role: {
      type: String,
      enum: ["customer", "admin"],
      default: "customer",
      index: true,
    },
    image: {
      type: String,
    },
  },
  { timestamps: true }
);

// Compound index for role-based queries
userSchema.index({ role: 1, createdAt: -1 });

export const User = mongoose.models.User || mongoose.model("User", userSchema);
