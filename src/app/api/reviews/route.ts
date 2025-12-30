import { errorLogger } from "@/lib/logger";
import { connectDB } from "@/lib/mongodb";
import { Review } from "@/models";
import { NextRequest, NextResponse } from "next/server";

export async function GET(req: NextRequest) {
  try {
    await connectDB();

    const { searchParams } = new URL(req.url);
    const menuItemId = searchParams.get("menuItemId");
    const orderId = searchParams.get("orderId");
    const approved = searchParams.get("approved");

    const filter: any = {};

    if (menuItemId) {
      filter.menuItem = menuItemId;
    }

    if (orderId) {
      filter.order = orderId;
    }

    if (approved === "true") {
      filter.isApproved = true;
    } else if (approved === "false") {
      filter.isApproved = false;
    }

    const reviews = await Review.find(filter)
      .populate("customer", "name email")
      .populate("menuItem", "name")
      .sort({ createdAt: -1 })
      .lean();

    return NextResponse.json({ reviews });
  } catch (error) {
    errorLogger.error(
      "Error fetching reviews",
      {},
      error instanceof Error ? error : new Error(String(error))
    );
    return NextResponse.json(
      { error: "Failed to fetch reviews" },
      { status: 500 }
    );
  }
}

export async function POST(req: NextRequest) {
  try {
    // Get user ID from authorization header or session
    const authHeader = req.headers.get("authorization");
    let userId = null;

    if (authHeader?.startsWith("Bearer ")) {
      // If using token, extract user ID from token
      userId = authHeader.substring(7);
    } else {
      // Fallback: Try to get from session (less reliable in API routes)
      try {
        const sessionRes = await fetch(
          "http://localhost:3000/api/auth/session",
          {
            headers: { cookie: req.headers.get("cookie") || "" },
          }
        );
        const session = await sessionRes.json();
        userId = session?.user?.id;
      } catch {
        // Session fetch failed, will require token
      }
    }

    if (!userId) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }

    await connectDB();

    const { orderId, menuItemId, rating, comment } = await req.json();

    // Validate input
    if (!orderId || !menuItemId || !rating) {
      return NextResponse.json(
        { error: "Missing required fields" },
        { status: 400 }
      );
    }

    if (rating < 1 || rating > 5) {
      return NextResponse.json(
        { error: "Rating must be between 1 and 5" },
        { status: 400 }
      );
    }

    // Check if review already exists for this order and menu item
    const existingReview = await Review.findOne({
      order: orderId,
      customer: userId,
      menuItem: menuItemId,
    });

    if (existingReview) {
      return NextResponse.json(
        { error: "You have already reviewed this item for this order" },
        { status: 400 }
      );
    }

    // Create review
    const review = new Review({
      order: orderId,
      customer: userId,
      menuItem: menuItemId,
      rating,
      comment: comment?.trim() || undefined,
      isApproved: false, // Default to pending approval
    });

    await review.save();

    errorLogger.info("Review created", {
      reviewId: review._id,
      menuItemId,
      rating,
    });

    return NextResponse.json(
      {
        success: true,
        review: review.toObject(),
      },
      { status: 201 }
    );
  } catch (error) {
    errorLogger.error(
      "Error creating review",
      {},
      error instanceof Error ? error : new Error(String(error))
    );
    return NextResponse.json(
      { error: "Failed to create review" },
      { status: 500 }
    );
  }
}
