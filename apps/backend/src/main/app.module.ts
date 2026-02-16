import { Module } from "@nestjs/common";
import { ConfigModule } from "@nestjs/config";
import { ThrottlerModule } from "@nestjs/throttler";
import { DatabaseModule } from "../database/database.module";
import { AdminModule } from "../modules/admin/admin.module";
import { AuthModule } from "../modules/auth/auth.module";
import { LocationsModule } from "../modules/locations/locations.module";
import { LoyaltyModule } from "../modules/loyalty/loyalty.module";
import { NotificationsModule } from "../modules/notifications/notifications.module";
import { OrdersModule } from "../modules/orders/orders.module";
import { ProductsModule } from "../modules/products/products.module";
import { ReviewsModule } from "../modules/reviews/reviews.module";
import { UsersModule } from "../modules/users/users.module";
import { WebsocketModule } from "../websocket/websocket.module";

@Module({
  imports: [
    // Configuration
    ConfigModule.forRoot({
      isGlobal: true,
      envFilePath: ".env",
    }),

    // Rate limiting
    ThrottlerModule.forRoot([
      {
        ttl: parseInt(process.env.THROTTLE_TTL || "60000"),
        limit: parseInt(process.env.THROTTLE_LIMIT || "100"),
      },
    ]),

    // Database
    DatabaseModule,

    // WebSocket (global)
    WebsocketModule,

    // Feature modules
    AuthModule,
    UsersModule,
    ProductsModule,
    OrdersModule,
    LocationsModule,
    NotificationsModule,
    ReviewsModule,
    LoyaltyModule,
    AdminModule,
  ],
})
export class AppModule {}
