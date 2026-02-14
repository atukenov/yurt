import { Prop, Schema, SchemaFactory } from "@nestjs/mongoose";
import { HydratedDocument, Types } from "mongoose";

export type OrderDocument = HydratedDocument<Order>;

@Schema()
export class OrderItem {
  @Prop({ type: Types.ObjectId, ref: "MenuItem", required: true })
  menuItem: Types.ObjectId;

  @Prop({ required: true, min: 1 })
  quantity: number;

  @Prop({ required: true, enum: ["small", "medium", "large"] })
  size: string;

  @Prop([{ type: Types.ObjectId, ref: "Topping" }])
  toppings: Types.ObjectId[];

  @Prop()
  specialInstructions: string;

  @Prop({ required: true })
  priceAtOrder: number;
}

export const OrderItemSchema = SchemaFactory.createForClass(OrderItem);

@Schema({ timestamps: true })
export class Order {
  @Prop({ unique: true, required: true })
  orderNumber: string;

  @Prop({ type: Types.ObjectId, ref: "User", required: true })
  customer: Types.ObjectId;

  @Prop({ type: Types.ObjectId, ref: "Location", required: true })
  location: Types.ObjectId;

  @Prop({ type: [OrderItemSchema] })
  items: OrderItem[];

  @Prop({
    enum: ["pending", "accepted", "rejected", "completed", "cancelled"],
    default: "pending",
  })
  status: string;

  @Prop({ required: true })
  totalPrice: number;

  @Prop()
  estimatedPrepTime: number;

  @Prop({
    enum: [
      "no_milk",
      "no_coffee_beans",
      "size_unavailable",
      "equipment_issue",
      "custom",
    ],
  })
  rejectionReason: string;

  @Prop()
  rejectionComment: string;

  @Prop({ enum: ["pending", "completed", "failed"], default: "pending" })
  paymentStatus: string;

  @Prop({ enum: ["kaspi", "applepay"], default: "kaspi" })
  paymentMethod: string;

  @Prop()
  notes: string;
}

export const OrderSchema = SchemaFactory.createForClass(Order);

// Indexes for faster queries
OrderSchema.index({ customer: 1, createdAt: -1 });
OrderSchema.index({ location: 1, status: 1, createdAt: -1 });
OrderSchema.index({ status: 1, createdAt: -1 });
OrderSchema.index({ createdAt: -1 });
OrderSchema.index({ paymentMethod: 1, createdAt: -1 });
OrderSchema.index({ customer: 1, status: 1 });
