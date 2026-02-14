import { Module } from "@nestjs/common";
import { MongooseModule } from "@nestjs/mongoose";
import {
  Location,
  LocationSchema,
  Loyalty,
  LoyaltySchema,
  MenuItem,
  MenuItemSchema,
  Notification,
  NotificationSchema,
  Order,
  OrderSchema,
  Topping,
  ToppingSchema,
  User,
  UserSchema,
} from "../../database/schemas";
import { AdminController } from "./admin.controller";
import { AdminService } from "./admin.service";

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: Order.name, schema: OrderSchema },
      { name: MenuItem.name, schema: MenuItemSchema },
      { name: Topping.name, schema: ToppingSchema },
      { name: Location.name, schema: LocationSchema },
      { name: Notification.name, schema: NotificationSchema },
      { name: Loyalty.name, schema: LoyaltySchema },
      { name: User.name, schema: UserSchema },
    ]),
  ],
  controllers: [AdminController],
  providers: [AdminService],
})
export class AdminModule {}
