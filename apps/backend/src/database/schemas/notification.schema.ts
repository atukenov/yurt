import { Prop, Schema, SchemaFactory } from "@nestjs/mongoose";
import { HydratedDocument, Types } from "mongoose";

export type NotificationDocument = HydratedDocument<Notification>;

@Schema({ timestamps: true })
export class Notification {
  @Prop({ type: Types.ObjectId, ref: "Order", required: true })
  order: Types.ObjectId;

  @Prop({ type: Types.ObjectId, ref: "User", required: true })
  recipient: Types.ObjectId;

  @Prop({
    required: true,
    enum: ["order_accepted", "order_rejected", "order_completed"],
  })
  type: string;

  @Prop()
  message: string;

  @Prop({ default: false })
  read: boolean;
}

export const NotificationSchema = SchemaFactory.createForClass(Notification);

// Indexes for faster queries
NotificationSchema.index({ recipient: 1, createdAt: -1 });
NotificationSchema.index({ recipient: 1, read: 1 });
NotificationSchema.index({ read: 1, createdAt: -1 });
NotificationSchema.index({ order: 1 });
