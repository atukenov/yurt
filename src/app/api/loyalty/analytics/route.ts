import { authOptions } from "@/lib/auth";
import { connectDB } from "@/lib/mongodb";
import { Loyalty } from "@/models";
import { getServerSession } from "next-auth/next";

export async function GET(request: Request) {
  try {
    await connectDB();
    const session = await getServerSession(authOptions);

    // Check if user is admin
    if (!session?.user?.id || session.user.role !== "admin") {
      return Response.json({ error: "Unauthorized" }, { status: 401 });
    }

    const url = new URL(request.url);
    const timeRange = url.searchParams.get("timeRange") || "30d";

    // Calculate date range
    const now = new Date();
    const daysAgo = timeRange === "7d" ? 7 : timeRange === "90d" ? 90 : 30;
    const startDate = new Date(now.getTime() - daysAgo * 24 * 60 * 60 * 1000);

    // Get all loyalty records with user info
    const loyalties = await Loyalty.find().populate("user", "name email");

    // Calculate statistics
    const totalMembers = loyalties.length;
    const totalPointsIssued = loyalties.reduce(
      (sum, l) => sum + (l.totalPoints || 0),
      0
    );
    const totalPointsRedeemed = loyalties.reduce(
      (sum, l) =>
        sum +
        (l.redemptionHistory?.reduce(
          (s: number, r: any) => s + (r.points || 0),
          0
        ) || 0),
      0
    );

    const totalDiscountsGiven = Math.floor(totalPointsRedeemed / 100);
    const avgPointsPerCustomer =
      totalMembers > 0 ? Math.round(totalPointsIssued / totalMembers) : 0;
    const avgSpendPerCustomer =
      totalMembers > 0
        ? parseFloat(
            (
              loyalties.reduce((sum, l) => sum + (l.totalSpent || 0), 0) /
              totalMembers
            ).toFixed(2)
          )
        : 0;

    // Tier distribution
    const tierCounts: { [key: string]: number } = {
      bronze: 0,
      silver: 0,
      gold: 0,
      platinum: 0,
    };

    const tierStats: { [key: string]: { points: number; count: number } } = {
      bronze: { points: 0, count: 0 },
      silver: { points: 0, count: 0 },
      gold: { points: 0, count: 0 },
      platinum: { points: 0, count: 0 },
    };

    loyalties.forEach((loyalty) => {
      const tier = loyalty.tier || "bronze";
      tierCounts[tier]++;
      tierStats[tier].points += loyalty.totalPoints || 0;
      tierStats[tier].count++;
    });

    const tierDistribution = Object.keys(tierCounts).map((tier) => ({
      tier,
      count: tierCounts[tier],
      percentage:
        totalMembers > 0
          ? Math.round((tierCounts[tier] / totalMembers) * 100)
          : 0,
      totalPoints: tierStats[tier].points,
      avgPoints:
        tierStats[tier].count > 0
          ? Math.round(tierStats[tier].points / tierStats[tier].count)
          : 0,
    }));

    // Top customers
    const topCustomers = loyalties
      .sort((a, b) => (b.totalSpent || 0) - (a.totalSpent || 0))
      .slice(0, 10)
      .map((loyalty) => ({
        userId: loyalty.user._id.toString(),
        name: (loyalty.user.name as string) || "Unknown",
        tier: loyalty.tier || "bronze",
        totalPoints: loyalty.totalPoints || 0,
        totalSpent: loyalty.totalSpent || 0,
        orderCount: loyalty.orderCount || 0,
        pointsRedeemed:
          loyalty.redemptionHistory?.reduce(
            (sum: number, r: any) => sum + (r.points || 0),
            0
          ) || 0,
      }));

    // Redemption trends (last 14 days)
    const redemptionTrends: {
      [key: string]: { count: number; points: number; discount: number };
    } = {};

    loyalties.forEach((loyalty) => {
      if (loyalty.redemptionHistory) {
        loyalty.redemptionHistory.forEach((redemption: any) => {
          if (redemption.redeemedAt && redemption.redeemedAt >= startDate) {
            const date = new Date(redemption.redeemedAt)
              .toISOString()
              .split("T")[0];
            if (!redemptionTrends[date]) {
              redemptionTrends[date] = { count: 0, points: 0, discount: 0 };
            }
            redemptionTrends[date].count++;
            redemptionTrends[date].points += redemption.points || 0;
            redemptionTrends[date].discount += redemption.discount || 0;
          }
        });
      }
    });

    const redemptionTrendArray = Object.entries(redemptionTrends)
      .sort(([dateA], [dateB]) => dateB.localeCompare(dateA))
      .slice(0, 14)
      .map(([date, data]) => ({
        date,
        count: data.count,
        totalPointsRedeemed: data.points,
        totalDiscountGiven: data.discount,
      }));

    // Birthday bonuses (this month)
    const currentMonth = now.getMonth();
    const birthdayBonusesAwarded = loyalties.filter((loyalty) => {
      if (!loyalty.birthday) return false;
      const birthdayMonth = new Date(loyalty.birthday).getMonth();
      return birthdayMonth === currentMonth && !loyalty.birthdayBonusUsed;
    }).length;

    return Response.json({
      totalMembers,
      totalPointsIssued,
      totalPointsRedeemed,
      totalDiscountsGiven,
      avgPointsPerCustomer,
      avgSpendPerCustomer,
      tierDistribution,
      topCustomers,
      redemptionTrends: redemptionTrendArray,
      birthdayBonusesAwarded,
    });
  } catch (error) {
    console.error("Error fetching loyalty analytics:", error);
    return Response.json(
      { error: "Failed to fetch analytics" },
      { status: 500 }
    );
  }
}
