import { Prop, Schema, SchemaFactory } from "@nestjs/mongoose";
import { HydratedDocument } from "mongoose";

export type ToppingDocument = HydratedDocument<Topping>;

@Schema({ timestamps: true })
export class Topping {
  @Prop({ required: true, index: true })
  name: string;

  @Prop({ required: true, default: 0 })
  price: number;

  @Prop({
    enum: ["syrup", "shot", "milk", "topping"],
    default: "topping",
    index: true,
  })
  category: string;

  @Prop()
  description: string;
}

export const ToppingSchema = SchemaFactory.createForClass(Topping);

// Compound index for category browsing
ToppingSchema.index({ category: 1, name: 1 });
