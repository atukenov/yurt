import mongoose, { ObjectId } from "mongoose";

const ReviewSchema = new mongoose.Schema(
  {
    restaurant: {
      type: mongoose.Schema.Types.ObjectId,
      ref: "profiles",
      required: true,
    },
    customer: {
      type: mongoose.Schema.Types.ObjectId,
      ref: "customers",
      required: true,
    },
    address: { type: String, required: true },
    rating: { type: Number, required: true, min: 1, max: 5 },
    comment: { type: String, required: true, maxlength: 500 },
  },
  { timestamps: true }
);

// Index for efficient queries
ReviewSchema.index({ restaurant: 1, address: 1, createdAt: -1 });
ReviewSchema.index({ customer: 1, restaurant: 1, address: 1 });

export const Reviews =
  mongoose.models?.reviews ?? mongoose.model<TReview>("reviews", ReviewSchema);

export type TReview = {
  _id: ObjectId;
  restaurant: ObjectId;
  customer:
    | ObjectId
    | {
        _id: ObjectId;
        fname: string;
        lname: string;
        phone: string;
      };
  address: string;
  rating: number;
  comment: string;
  createdAt: Date;
  updatedAt: Date;
};
