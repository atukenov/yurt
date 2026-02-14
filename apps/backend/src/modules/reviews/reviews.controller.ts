import {
  Body,
  Controller,
  Delete,
  Get,
  HttpCode,
  HttpStatus,
  Param,
  Post,
  Put,
  Query,
  UseGuards,
} from "@nestjs/common";
import { CurrentUser } from "../../common/decorators/current-user.decorator";
import { JwtAuthGuard } from "../../common/guards/jwt-auth.guard";
import { ReviewsService } from "./reviews.service";

@Controller("reviews")
export class ReviewsController {
  constructor(private readonly reviewsService: ReviewsService) {}

  @Get()
  async getReviews(
    @Query("menuItemId") menuItemId?: string,
    @Query("orderId") orderId?: string,
    @Query("approved") approved?: string
  ) {
    return this.reviewsService.getReviews({ menuItemId, orderId, approved });
  }

  @Post()
  @UseGuards(JwtAuthGuard)
  @HttpCode(HttpStatus.CREATED)
  async createReview(@CurrentUser("id") userId: string, @Body() body: any) {
    return this.reviewsService.createReview(userId, body);
  }

  @Put(":id")
  @UseGuards(JwtAuthGuard)
  async updateApproval(@Param("id") id: string, @Body() body: any) {
    return this.reviewsService.updateReviewApproval(id, body.isApproved);
  }

  @Delete(":id")
  @UseGuards(JwtAuthGuard)
  async deleteReview(@Param("id") id: string) {
    return this.reviewsService.deleteReview(id);
  }
}
