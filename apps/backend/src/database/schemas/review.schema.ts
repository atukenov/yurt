import { Prop, Schema, SchemaFactory } from "@nestjs/mongoose";
import { HydratedDocument, Types } from "mongoose";

export type ReviewDocument = HydratedDocument<Review>;

@Schema({ timestamps: true })
export class Review {
  @Prop({ type: Types.ObjectId, ref: "Order", required: true, index: true })
  order: Types.ObjectId;

  @Prop({ type: Types.ObjectId, ref: "User", required: true, index: true })
  customer: Types.ObjectId;

  @Prop({ type: Types.ObjectId, ref: "MenuItem", required: true, index: true })
  menuItem: Types.ObjectId;

  @Prop({ required: true, min: 1, max: 5 })
  rating: number;

  @Prop()
  comment: string;

  @Prop({ default: false, index: true })
  isApproved: boolean;
}

export const ReviewSchema = SchemaFactory.createForClass(Review);

// Compound indexes for common queries
ReviewSchema.index({ menuItem: 1, isApproved: 1 });
ReviewSchema.index({ isApproved: 1, createdAt: -1 });
ReviewSchema.index({ customer: 1, createdAt: -1 });
