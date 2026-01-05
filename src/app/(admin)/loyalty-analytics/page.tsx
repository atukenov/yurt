"use client";

import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

interface TierStats {
  tier: string;
  count: number;
  percentage: number;
  totalPoints: number;
  avgPoints: number;
}

interface TopCustomer {
  userId: string;
  name: string;
  tier: string;
  totalPoints: number;
  totalSpent: number;
  orderCount: number;
  pointsRedeemed: number;
}

interface RedemptionTrend {
  date: string;
  count: number;
  totalPointsRedeemed: number;
  totalDiscountGiven: number;
}

interface LoyaltyStats {
  totalMembers: number;
  totalPointsIssued: number;
  totalPointsRedeemed: number;
  totalDiscountsGiven: number;
  avgPointsPerCustomer: number;
  avgSpendPerCustomer: number;
  tierDistribution: TierStats[];
  topCustomers: TopCustomer[];
  redemptionTrends: RedemptionTrend[];
  birthdayBonusesAwarded: number;
}

export default function LoyaltyAnalyticsPage() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [stats, setStats] = useState<LoyaltyStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [timeRange, setTimeRange] = useState<"7d" | "30d" | "90d">("30d");

  useEffect(() => {
    if (status === "unauthenticated") {
      router.push("/login");
      return;
    }

    if (status === "authenticated" && session?.user?.role !== "admin") {
      router.push("/");
      return;
    }

    if (status === "authenticated") {
      fetchAnalytics();
    }
  }, [status, session, timeRange]);

  const fetchAnalytics = async () => {
    try {
      setLoading(true);
      const response = await fetch(
        `/api/loyalty/analytics?timeRange=${timeRange}`
      );
      if (!response.ok) throw new Error("Failed to fetch analytics");
      const data = await response.json();
      setStats(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "An error occurred");
    } finally {
      setLoading(false);
    }
  };

  if (status === "loading" || loading) {
    return (
      <div className="p-6">
        <div className="animate-pulse space-y-4">
          <div className="h-8 bg-gray-200 rounded w-1/3"></div>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="h-24 bg-gray-200 rounded"></div>
            ))}
          </div>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="p-6">
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-800">
          Error loading analytics: {error}
        </div>
      </div>
    );
  }

  if (!stats) {
    return (
      <div className="p-6">
        <div className="text-gray-500">No analytics data available</div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex justify-between items-center">
        <h1 className="text-3xl font-bold">Loyalty Analytics</h1>
        <div className="flex gap-2">
          {(["7d", "30d", "90d"] as const).map((range) => (
            <button
              key={range}
              onClick={() => setTimeRange(range)}
              className={`px-4 py-2 rounded-lg font-medium transition ${
                timeRange === range
                  ? "bg-[#ffd119] text-black"
                  : "bg-gray-200 text-gray-700 hover:bg-gray-300"
              }`}
            >
              {range === "7d"
                ? "7 Days"
                : range === "30d"
                ? "30 Days"
                : "90 Days"}
            </button>
          ))}
        </div>
      </div>

      {/* Key Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-white rounded-lg shadow p-6">
          <div className="text-sm text-gray-600 mb-1">Total Members</div>
          <div className="text-3xl font-bold text-amber-600">
            {stats.totalMembers}
          </div>
          <div className="text-xs text-gray-500 mt-2">
            Active loyalty customers
          </div>
        </div>

        <div className="bg-white rounded-lg shadow p-6">
          <div className="text-sm text-gray-600 mb-1">Points Issued</div>
          <div className="text-3xl font-bold text-blue-600">
            {stats.totalPointsIssued.toLocaleString()}
          </div>
          <div className="text-xs text-gray-500 mt-2">
            Avg {Math.round(stats.avgPointsPerCustomer)} per customer
          </div>
        </div>

        <div className="bg-white rounded-lg shadow p-6">
          <div className="text-sm text-gray-600 mb-1">Points Redeemed</div>
          <div className="text-3xl font-bold text-green-600">
            {stats.totalPointsRedeemed.toLocaleString()}
          </div>
          <div className="text-xs text-gray-500 mt-2">
            Redemption rate:{" "}
            {stats.totalPointsIssued > 0
              ? Math.round(
                  (stats.totalPointsRedeemed / stats.totalPointsIssued) * 100
                )
              : 0}
            %
          </div>
        </div>

        <div className="bg-white rounded-lg shadow p-6">
          <div className="text-sm text-gray-600 mb-1">Total Discounts</div>
          <div className="text-3xl font-bold text-purple-600">
            ${stats.totalDiscountsGiven.toFixed(2)}
          </div>
          <div className="text-xs text-gray-500 mt-2">
            {stats.totalPointsRedeemed > 0
              ? (stats.totalPointsRedeemed / 100).toFixed(2)
              : "0"}{" "}
            points = $1
          </div>
        </div>
      </div>

      {/* Tier Distribution */}
      <div className="bg-white rounded-lg shadow p-6">
        <h2 className="text-xl font-bold mb-4">Customer Tier Distribution</h2>
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {stats.tierDistribution.map((tier) => (
            <div key={tier.tier} className="border rounded-lg p-4">
              <div className="flex items-center justify-between mb-2">
                <div className="font-semibold capitalize">{tier.tier}</div>
                <div className="text-sm bg-gray-100 px-2 py-1 rounded">
                  {tier.percentage}%
                </div>
              </div>
              <div className="space-y-1 text-sm">
                <div className="text-gray-600">
                  <span className="font-medium">{tier.count}</span> customers
                </div>
                <div className="text-gray-600">
                  <span className="font-medium">
                    {tier.totalPoints.toLocaleString()}
                  </span>{" "}
                  total points
                </div>
                <div className="text-gray-600">
                  <span className="font-medium">
                    {tier.avgPoints.toLocaleString()}
                  </span>{" "}
                  avg per customer
                </div>
              </div>
              <div className="mt-3">
                <div className="w-full bg-gray-200 rounded-full h-2">
                  <div
                    className="h-2 rounded-full transition-all"
                    style={{
                      width: `${tier.percentage}%`,
                      backgroundColor:
                        tier.tier === "bronze"
                          ? "#8B4513"
                          : tier.tier === "silver"
                          ? "#C0C0C0"
                          : tier.tier === "gold"
                          ? "#FFD700"
                          : "#9C27B0",
                    }}
                  ></div>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Top Customers */}
      <div className="bg-white rounded-lg shadow p-6">
        <h2 className="text-xl font-bold mb-4">Top Customers</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b">
              <tr className="text-left text-gray-600">
                <th className="pb-3">Name</th>
                <th className="pb-3">Tier</th>
                <th className="pb-3 text-right">Total Spent</th>
                <th className="pb-3 text-right">Orders</th>
                <th className="pb-3 text-right">Points Earned</th>
                <th className="pb-3 text-right">Points Redeemed</th>
              </tr>
            </thead>
            <tbody>
              {stats.topCustomers.map((customer) => (
                <tr key={customer.userId} className="border-b hover:bg-gray-50">
                  <td className="py-3 font-medium">{customer.name}</td>
                  <td className="py-3">
                    <span
                      className="px-2 py-1 rounded-full text-xs font-semibold capitalize"
                      style={{
                        backgroundColor:
                          customer.tier === "bronze"
                            ? "#D2B48C"
                            : customer.tier === "silver"
                            ? "#E8E8E8"
                            : customer.tier === "gold"
                            ? "#FFF8DC"
                            : "#E6D5E8",
                      }}
                    >
                      {customer.tier}
                    </span>
                  </td>
                  <td className="py-3 text-right font-medium">
                    ${customer.totalSpent.toFixed(2)}
                  </td>
                  <td className="py-3 text-right">{customer.orderCount}</td>
                  <td className="py-3 text-right text-blue-600 font-medium">
                    {customer.totalPoints.toLocaleString()}
                  </td>
                  <td className="py-3 text-right text-green-600 font-medium">
                    {customer.pointsRedeemed.toLocaleString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Redemption Trends */}
      <div className="bg-white rounded-lg shadow p-6">
        <h2 className="text-xl font-bold mb-4">Redemption Trends</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="border-b">
              <tr className="text-left text-gray-600">
                <th className="pb-3">Date</th>
                <th className="pb-3 text-right">Redemptions</th>
                <th className="pb-3 text-right">Points Redeemed</th>
                <th className="pb-3 text-right">Discount Given</th>
              </tr>
            </thead>
            <tbody>
              {stats.redemptionTrends.map((trend) => (
                <tr key={trend.date} className="border-b hover:bg-gray-50">
                  <td className="py-3 font-medium">{trend.date}</td>
                  <td className="py-3 text-right">{trend.count}</td>
                  <td className="py-3 text-right text-blue-600 font-medium">
                    {trend.totalPointsRedeemed.toLocaleString()}
                  </td>
                  <td className="py-3 text-right text-green-600 font-medium">
                    ${trend.totalDiscountGiven.toFixed(2)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Additional Info */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div className="bg-blue-50 rounded-lg p-4 border border-blue-200">
          <div className="text-sm font-semibold text-blue-900 mb-2">
            Average Spend Per Customer
          </div>
          <div className="text-2xl font-bold text-blue-600">
            ${stats.avgSpendPerCustomer.toFixed(2)}
          </div>
        </div>

        <div className="bg-purple-50 rounded-lg p-4 border border-purple-200">
          <div className="text-sm font-semibold text-purple-900 mb-2">
            Birthday Bonuses Awarded
          </div>
          <div className="text-2xl font-bold text-purple-600">
            {stats.birthdayBonusesAwarded}
          </div>
        </div>
      </div>
    </div>
  );
}
