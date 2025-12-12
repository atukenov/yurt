import { NextRequest, NextResponse } from "next/server";

import { getServerSession } from "next-auth";

import { authOptions } from "#utils/helper/authHelper";
import connectDB from "#utils/database/connect";
import { Reviews } from "#utils/database/models/review";
import { Profiles } from "#utils/database/models/profile";

// GET - Fetch reviews for a restaurant and address
export async function GET(req: NextRequest) {
  try {
    await connectDB();

    const { searchParams } = new URL(req.url);
    const restaurantUsername = searchParams.get("restaurant");
    const address = searchParams.get("address");

    if (!restaurantUsername || !address) {
      return NextResponse.json(
        { error: "Restaurant and address are required" },
        { status: 400 }
      );
    }

    // Find restaurant
    const restaurant = await Profiles.findOne({
      restaurantID: restaurantUsername,
    });
    if (!restaurant) {
      return NextResponse.json(
        { error: "Restaurant not found" },
        { status: 404 }
      );
    }

    // Fetch reviews
    const reviews = await Reviews.find({
      restaurant: restaurant._id,
      address,
    })
      .populate("customer", "fname lname")
      .sort({ createdAt: -1 })
      .lean();

    // Calculate average rating
    const avgRating =
      reviews.length > 0
        ? reviews.reduce((sum, review) => sum + review.rating, 0) /
          reviews.length
        : 0;

    return NextResponse.json({
      reviews,
      avgRating: Math.round(avgRating * 10) / 10,
      totalReviews: reviews.length,
    });
  } catch (error) {
    console.error("Error fetching reviews:", error);
    return NextResponse.json(
      { error: "Failed to fetch reviews" },
      { status: 500 }
    );
  }
}

// POST - Create a new review
export async function POST(req: NextRequest) {
  try {
    const session = await getServerSession(authOptions);

    if (!session || session.role !== "customer") {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }

    await connectDB();

    const body = await req.json();
    const { restaurant: restaurantUsername, address, rating, comment } = body;

    if (!restaurantUsername || !address || !rating || !comment) {
      return NextResponse.json(
        { error: "All fields are required" },
        { status: 400 }
      );
    }

    if (rating < 1 || rating > 5) {
      return NextResponse.json(
        { error: "Rating must be between 1 and 5" },
        { status: 400 }
      );
    }

    if (comment.length > 500) {
      return NextResponse.json(
        { error: "Comment must be 500 characters or less" },
        { status: 400 }
      );
    }

    // Find restaurant
    const restaurant = await Profiles.findOne({
      restaurantID: restaurantUsername,
    });
    if (!restaurant) {
      return NextResponse.json(
        { error: "Restaurant not found" },
        { status: 404 }
      );
    }

    // Check review limit (max 3 reviews per customer per restaurant/address)
    const existingReviews = await Reviews.countDocuments({
      restaurant: restaurant._id,
      customer: session.customer?._id,
      address,
    });

    if (existingReviews >= 3) {
      return NextResponse.json(
        { error: "You can only leave up to 3 reviews for this location" },
        { status: 400 }
      );
    }

    // Create review
    const review = await Reviews.create({
      restaurant: restaurant._id,
      customer: session.customer?._id,
      address,
      rating,
      comment,
    });

    const populatedReview = await Reviews.findById(review._id)
      .populate("customer", "fname lname")
      .lean();

    return NextResponse.json({
      message: "Review created successfully",
      review: populatedReview,
    });
  } catch (error) {
    console.error("Error creating review:", error);
    return NextResponse.json(
      { error: "Failed to create review" },
      { status: 500 }
    );
  }
}
