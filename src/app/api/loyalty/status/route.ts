import { authOptions } from "@/lib/auth";
import { errorLogger } from "@/lib/logger";
import { connectDB } from "@/lib/mongodb";
import { Loyalty } from "@/models";
import { getServerSession } from "next-auth";
import { NextResponse } from "next/server";

export async function GET(req: Request) {
  try {
    const session = await getServerSession(authOptions);
    if (!session?.user?.id) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }

    await connectDB();

    // Find or create loyalty record
    let loyalty = await Loyalty.findOne({ user: session.user.id }).populate(
      "user",
      "name email"
    );

    if (!loyalty) {
      loyalty = await Loyalty.create({ user: session.user.id });
    }

    return NextResponse.json({
      success: true,
      data: {
        totalPoints: loyalty.totalPoints,
        availablePoints: loyalty.availablePoints,
        tier: loyalty.tier,
        totalSpent: loyalty.totalSpent,
        orderCount: loyalty.orderCount,
        lastOrderDate: loyalty.lastOrderDate,
        birthday: loyalty.birthday,
        tierBenefits: loyalty.getTierBenefits(),
      },
    });
  } catch (error) {
    errorLogger.log("error", "GET /api/loyalty/status", {}, error as Error);
    return NextResponse.json(
      { error: "Failed to fetch loyalty status" },
      { status: 500 }
    );
  }
}
