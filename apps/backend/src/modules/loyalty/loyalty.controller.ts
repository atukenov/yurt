import { Body, Controller, Get, Post, UseGuards } from "@nestjs/common";
import { CurrentUser } from "../../common/decorators/current-user.decorator";
import { JwtAuthGuard } from "../../common/guards/jwt-auth.guard";
import { LoyaltyService } from "./loyalty.service";

@Controller("loyalty")
@UseGuards(JwtAuthGuard)
export class LoyaltyController {
  constructor(private readonly loyaltyService: LoyaltyService) {}

  @Get("status")
  async getStatus(@CurrentUser("id") userId: string) {
    return this.loyaltyService.getStatus(userId);
  }

  @Post("award")
  async awardPoints(@CurrentUser("id") userId: string, @Body() body: any) {
    return this.loyaltyService.awardPoints(
      userId,
      body.orderId,
      body.orderAmount
    );
  }

  @Post("redeem")
  async redeemPoints(@CurrentUser("id") userId: string, @Body() body: any) {
    return this.loyaltyService.redeemPoints(userId, body.points, body.orderId);
  }
}
