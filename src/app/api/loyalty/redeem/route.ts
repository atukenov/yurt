import { authOptions } from "@/lib/auth";
import { errorLogger } from "@/lib/logger";
import { connectDB } from "@/lib/mongodb";
import { Loyalty } from "@/models";
import { getServerSession } from "next-auth";
import { NextResponse } from "next/server";

interface RedeemPointsRequest {
  points: number;
  orderId: string;
}

export async function POST(req: Request) {
  try {
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }

    const body: RedeemPointsRequest = await req.json();
    const { points, orderId } = body;

    if (!points || !orderId || points <= 0 || !Number.isInteger(points)) {
      return NextResponse.json(
        { error: "Invalid points or orderId" },
        { status: 400 }
      );
    }

    await connectDB();

    const loyalty = await Loyalty.findOne({ user: session.user.id });
    if (!loyalty) {
      return NextResponse.json(
        { error: "Loyalty account not found" },
        { status: 404 }
      );
    }

    // Check if user has enough points
    if (points > loyalty.availablePoints) {
      return NextResponse.json(
        {
          error: "Insufficient points",
          availablePoints: loyalty.availablePoints,
        },
        { status: 400 }
      );
    }

    // Redeem points
    const discount = loyalty.redeemPoints(points, orderId);

    await loyalty.save();

    return NextResponse.json({
      success: true,
      data: {
        discount,
        pointsRedeemed: points,
        remainingPoints: loyalty.availablePoints,
      },
    });
  } catch (error) {
    errorLogger.log("error", "POST /api/loyalty/redeem", {}, error as Error);
    return NextResponse.json(
      { error: "Failed to redeem loyalty points" },
      { status: 500 }
    );
  }
}
