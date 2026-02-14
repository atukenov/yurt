import { Prop, Schema, SchemaFactory, raw } from "@nestjs/mongoose";
import { HydratedDocument } from "mongoose";

export type MenuItemDocument = HydratedDocument<MenuItem>;

@Schema({ timestamps: true })
export class MenuItem {
  @Prop({ required: true })
  name: string;

  @Prop()
  description: string;

  @Prop({
    required: true,
    enum: ["hot", "cold", "latte", "cappuccino", "espresso", "specialty"],
  })
  category: string;

  @Prop({ required: true })
  basePrice: number;

  @Prop([
    raw({
      size: { type: String, enum: ["small", "medium", "large"] },
      priceModifier: { type: Number },
    }),
  ])
  sizes: Record<string, any>[];

  @Prop()
  image: string;

  @Prop({ default: true })
  isAvailable: boolean;

  @Prop({ default: 5 })
  preparationTime: number;
}

export const MenuItemSchema = SchemaFactory.createForClass(MenuItem);

// Indexes for faster searches and queries
MenuItemSchema.index({ name: "text", category: 1 });
MenuItemSchema.index({ category: 1, isAvailable: 1 });
MenuItemSchema.index({ isAvailable: 1, createdAt: -1 });
MenuItemSchema.index({ basePrice: 1 });
MenuItemSchema.index({ category: 1, basePrice: 1 });
