import {
  BadRequestException,
  Injectable,
  NotFoundException,
} from "@nestjs/common";
import { InjectModel } from "@nestjs/mongoose";
import { Model } from "mongoose";
import { Review, ReviewDocument } from "../../database/schemas";

@Injectable()
export class ReviewsService {
  constructor(
    @InjectModel(Review.name) private reviewModel: Model<ReviewDocument>
  ) {}

  async getReviews(filters: {
    menuItemId?: string;
    orderId?: string;
    approved?: string;
  }) {
    const filter: any = {};
    if (filters.menuItemId) filter.menuItem = filters.menuItemId;
    if (filters.orderId) filter.order = filters.orderId;
    if (filters.approved === "true") filter.isApproved = true;
    else if (filters.approved === "false") filter.isApproved = false;

    const reviews = await this.reviewModel
      .find(filter)
      .populate("customer", "name email")
      .populate("menuItem", "name")
      .sort({ createdAt: -1 })
      .lean();

    return { reviews };
  }

  async createReview(
    userId: string,
    data: {
      orderId: string;
      menuItemId: string;
      rating: number;
      comment?: string;
    }
  ) {
    if (!data.orderId || !data.menuItemId || !data.rating) {
      throw new BadRequestException("Missing required fields");
    }
    if (data.rating < 1 || data.rating > 5) {
      throw new BadRequestException("Rating must be between 1 and 5");
    }

    const existing = await this.reviewModel.findOne({
      order: data.orderId,
      customer: userId,
      menuItem: data.menuItemId,
    });
    if (existing) {
      throw new BadRequestException(
        "You have already reviewed this item for this order"
      );
    }

    const review = new this.reviewModel({
      order: data.orderId,
      customer: userId,
      menuItem: data.menuItemId,
      rating: data.rating,
      comment: data.comment?.trim() || undefined,
      isApproved: false,
    });

    await review.save();
    return { success: true, review: review.toObject() };
  }

  async updateReviewApproval(reviewId: string, isApproved: boolean) {
    const review = await this.reviewModel
      .findByIdAndUpdate(reviewId, { isApproved }, { new: true })
      .populate("customer", "name email")
      .populate("menuItem", "name");
    if (!review) throw new NotFoundException("Review not found");
    return { review };
  }

  async deleteReview(reviewId: string) {
    const review = await this.reviewModel.findByIdAndDelete(reviewId);
    if (!review) throw new NotFoundException("Review not found");
    return { success: true };
  }
}
