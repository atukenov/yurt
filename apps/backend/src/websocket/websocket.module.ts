import { Global, Module } from "@nestjs/common";
import { OrdersGateway } from "./orders.gateway";

@Global()
@Module({
  providers: [OrdersGateway],
  exports: [OrdersGateway],
})
export class WebsocketModule {}
