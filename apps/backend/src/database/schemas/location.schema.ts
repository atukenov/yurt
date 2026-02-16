import { Prop, Schema, SchemaFactory, raw } from "@nestjs/mongoose";
import { HydratedDocument, Types } from "mongoose";

export type LocationDocument = HydratedDocument<Location>;

@Schema({ timestamps: true })
export class Location {
  @Prop({ required: true })
  name: string;

  @Prop({ required: true })
  address: string;

  @Prop({ required: true })
  city: string;

  @Prop({ required: true })
  zipCode: string;

  @Prop()
  phone: string;

  @Prop()
  latitude: number;

  @Prop()
  longitude: number;

  @Prop(
    raw({
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
    })
  )
  workingHours: Record<string, any>;

  @Prop({ default: true, index: true })
  isActive: boolean;

  @Prop([
    raw({
      date: { type: Date, required: true },
      name: { type: String },
      isClosed: { type: Boolean, default: true },
      customHours: {
        open: { type: String },
        close: { type: String },
      },
    }),
  ])
  holidays: Record<string, any>[];

  @Prop([{ type: Types.ObjectId, ref: "MenuItem" }])
  availableMenuItems: Types.ObjectId[];
}

export const LocationSchema = SchemaFactory.createForClass(Location);

// Indexes for location queries
LocationSchema.index({ name: 1 });
LocationSchema.index({ city: 1 });
LocationSchema.index({ isActive: 1, city: 1 });
