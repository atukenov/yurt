import { Module } from "@nestjs/common";
import { MongooseModule } from "@nestjs/mongoose";
import { Loyalty, LoyaltySchema } from "../../database/schemas";
import { LoyaltyController } from "./loyalty.controller";
import { LoyaltyService } from "./loyalty.service";

@Module({
  imports: [
    MongooseModule.forFeature([{ name: Loyalty.name, schema: LoyaltySchema }]),
  ],
  controllers: [LoyaltyController],
  providers: [LoyaltyService],
  exports: [LoyaltyService],
})
export class LoyaltyModule {}
