import { authOptions } from "@/lib/auth";
import { errorLogger } from "@/lib/logger";
import { connectDB } from "@/lib/mongodb";
import { Loyalty } from "@/models";
import { getServerSession } from "next-auth";
import { NextResponse } from "next/server";

interface AwardPointsRequest {
  orderId: string;
  orderAmount: number;
}

export async function POST(req: Request) {
  try {
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }

    const body: AwardPointsRequest = await req.json();
    const { orderId, orderAmount } = body;

    if (!orderId || typeof orderAmount !== "number" || orderAmount <= 0) {
      return NextResponse.json(
        { error: "Invalid orderId or orderAmount" },
        { status: 400 }
      );
    }

    await connectDB();

    // Find or create loyalty record
    let loyalty = await Loyalty.findOne({ user: session.user.id });
    if (!loyalty) {
      loyalty = await Loyalty.create({ user: session.user.id });
    }

    // Award points
    const pointsEarned = loyalty.awardPoints(orderAmount, orderId);

    // Save and return updated loyalty
    await loyalty.save();

    return NextResponse.json({
      success: true,
      data: {
        pointsEarned,
        totalPoints: loyalty.totalPoints,
        availablePoints: loyalty.availablePoints,
        tier: loyalty.tier,
        tierBenefits: loyalty.getTierBenefits(),
      },
    });
  } catch (error) {
    errorLogger.log("error", "POST /api/loyalty/award", {}, error as Error);
    return NextResponse.json(
      { error: "Failed to award loyalty points" },
      { status: 500 }
    );
  }
}
