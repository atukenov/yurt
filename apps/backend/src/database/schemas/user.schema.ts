import { Prop, Schema, SchemaFactory } from "@nestjs/mongoose";
import { HydratedDocument } from "mongoose";

export type UserDocument = HydratedDocument<User>;

@Schema({ timestamps: true })
export class User {
  @Prop({ required: true, unique: true, lowercase: true, index: true })
  email: string;

  @Prop({ required: true, minlength: 4, maxlength: 4 })
  pinCode: string;

  @Prop({ required: true, index: true })
  name: string;

  @Prop()
  firstName: string;

  @Prop()
  lastName: string;

  @Prop()
  phone: string;

  @Prop({ enum: ["customer", "admin"], default: "customer", index: true })
  role: string;

  @Prop()
  image: string;
}

export const UserSchema = SchemaFactory.createForClass(User);

// Compound index for role-based queries
UserSchema.index({ role: 1, createdAt: -1 });
