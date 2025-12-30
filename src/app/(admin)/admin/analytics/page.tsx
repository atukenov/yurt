"use client";

import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

interface AnalyticsData {
  totalOrders: number;
  totalRevenue: number;
  completedOrders: number;
  pendingOrders: number;
  averageOrderValue: number;
  populateMenuItems: Array<{
    name: string;
    count: number;
    revenue: number;
  }>;
  revenueByDay: Array<{
    date: string;
    revenue: number;
    orders: number;
  }>;
  peakHours: Array<{
    hour: number;
    count: number;
  }>;
  paymentMethodBreakdown: {
    cash: number;
    card: number;
    stripe: number;
  };
  topCustomers: Array<{
    name: string;
    email: string;
    orderCount: number;
    totalSpent: number;
  }>;
}

export default function AdminAnalyticsPage() {
  const router = useRouter();
  const { data: session, status } = useSession({
    required: true,
    onUnauthenticated() {
      router.push("/login");
    },
  });

  const [analytics, setAnalytics] = useState<AnalyticsData | null>(null);
  const [loading, setLoading] = useState(true);
  const [timeRange, setTimeRange] = useState<"7days" | "30days" | "90days">(
    "30days"
  );

  const fetchAnalytics = useCallback(async () => {
    try {
      setLoading(true);
      const res = await fetch(`/api/admin/analytics?timeRange=${timeRange}`);
      if (res.ok) {
        const data = await res.json();
        setAnalytics(data);
      } else {
        console.error("Failed to fetch analytics data");
      }
    } catch (error) {
      console.error("Error fetching analytics:", error);
    } finally {
      setLoading(false);
    }
  }, [timeRange]);

  // Effect for auth check
  useEffect(() => {
    if (status === "authenticated" && session?.user?.role !== "admin") {
      router.push("/");
    }
  }, [status, session?.user?.role, router]);

  // Effect for fetching analytics when timeRange changes
  useEffect(() => {
    if (status === "authenticated" && session?.user?.role === "admin") {
      fetchAnalytics();
    }
  }, [fetchAnalytics, status, session?.user?.role]);

  // Don't render anything while session is loading
  if (status === "loading") {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="text-center py-12">
          <p className="text-gray-600">Loading...</p>
        </div>
      </div>
    );
  }

  // Only render for authenticated admins
  if (status !== "authenticated" || session?.user?.role !== "admin") {
    return null;
  }

  if (loading) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="text-center py-12">
          <p className="text-gray-600">Loading analytics...</p>
        </div>
      </div>
    );
  }

  if (!analytics) {
    return (
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="text-center py-12">
          <p className="text-gray-600">No analytics data available</p>
        </div>
      </div>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      {/* Header */}
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-4">
          Analytics Dashboard
        </h1>
        <div className="flex gap-2">
          <button
            onClick={() => setTimeRange("7days")}
            className={`px-4 py-2 rounded-lg font-semibold transition ${
              timeRange === "7days"
                ? "bg-amber-600 text-white"
                : "bg-gray-200 text-gray-700 hover:bg-gray-300"
            }`}
          >
            Last 7 Days
          </button>
          <button
            onClick={() => setTimeRange("30days")}
            className={`px-4 py-2 rounded-lg font-semibold transition ${
              timeRange === "30days"
                ? "bg-amber-600 text-white"
                : "bg-gray-200 text-gray-700 hover:bg-gray-300"
            }`}
          >
            Last 30 Days
          </button>
          <button
            onClick={() => setTimeRange("90days")}
            className={`px-4 py-2 rounded-lg font-semibold transition ${
              timeRange === "90days"
                ? "bg-amber-600 text-white"
                : "bg-gray-200 text-gray-700 hover:bg-gray-300"
            }`}
          >
            Last 90 Days
          </button>
        </div>
      </div>

      {/* Key Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
        {/* Total Orders */}
        <div className="bg-white rounded-lg shadow p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-gray-600 mb-1">
                Total Orders
              </p>
              <p className="text-3xl font-bold text-gray-900">
                {analytics.totalOrders}
              </p>
            </div>
            <div className="text-4xl text-blue-500">📊</div>
          </div>
        </div>

        {/* Total Revenue */}
        <div className="bg-white rounded-lg shadow p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-gray-600 mb-1">
                Total Revenue
              </p>
              <p className="text-3xl font-bold text-green-600">
                ${analytics.totalRevenue.toFixed(2)}
              </p>
            </div>
            <div className="text-4xl text-green-500">💰</div>
          </div>
        </div>

        {/* Average Order Value */}
        <div className="bg-white rounded-lg shadow p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-gray-600 mb-1">
                Avg Order Value
              </p>
              <p className="text-3xl font-bold text-gray-900">
                ${analytics.averageOrderValue.toFixed(2)}
              </p>
            </div>
            <div className="text-4xl text-amber-500">💵</div>
          </div>
        </div>

        {/* Completion Rate */}
        <div className="bg-white rounded-lg shadow p-6">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-gray-600 mb-1">
                Completion Rate
              </p>
              <p className="text-3xl font-bold text-purple-600">
                {analytics.totalOrders > 0
                  ? (
                      (analytics.completedOrders / analytics.totalOrders) *
                      100
                    ).toFixed(0)
                  : 0}
                %
              </p>
            </div>
            <div className="text-4xl text-purple-500">✅</div>
          </div>
        </div>
      </div>

      {/* Orders Status */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
        {/* Order Status Breakdown */}
        <div className="bg-white rounded-lg shadow p-6">
          <h2 className="text-lg font-bold text-gray-900 mb-4">
            Order Status Breakdown
          </h2>
          <div className="space-y-4">
            <div className="flex items-center justify-between p-3 bg-blue-50 rounded-lg">
              <span className="text-sm font-medium text-gray-700">
                Pending Orders
              </span>
              <span className="text-2xl font-bold text-blue-600">
                {analytics.pendingOrders}
              </span>
            </div>
            <div className="flex items-center justify-between p-3 bg-green-50 rounded-lg">
              <span className="text-sm font-medium text-gray-700">
                Completed Orders
              </span>
              <span className="text-2xl font-bold text-green-600">
                {analytics.completedOrders}
              </span>
            </div>
          </div>
        </div>

        {/* Payment Method Breakdown */}
        <div className="bg-white rounded-lg shadow p-6">
          <h2 className="text-lg font-bold text-gray-900 mb-4">
            Payment Methods
          </h2>
          <div className="space-y-4">
            <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
              <span className="text-sm font-medium text-gray-700">Cash</span>
              <div className="flex items-center gap-2">
                <span className="text-2xl font-bold text-gray-900">
                  {analytics.paymentMethodBreakdown.cash}
                </span>
                <span className="text-xs text-gray-600">
                  (
                  {analytics.totalOrders > 0
                    ? (
                        (analytics.paymentMethodBreakdown.cash /
                          analytics.totalOrders) *
                        100
                      ).toFixed(0)
                    : 0}
                  %)
                </span>
              </div>
            </div>
            <div className="flex items-center justify-between p-3 bg-blue-50 rounded-lg">
              <span className="text-sm font-medium text-gray-700">Card</span>
              <div className="flex items-center gap-2">
                <span className="text-2xl font-bold text-blue-900">
                  {analytics.paymentMethodBreakdown.card}
                </span>
                <span className="text-xs text-gray-600">
                  (
                  {analytics.totalOrders > 0
                    ? (
                        (analytics.paymentMethodBreakdown.card /
                          analytics.totalOrders) *
                        100
                      ).toFixed(0)
                    : 0}
                  %)
                </span>
              </div>
            </div>
            <div className="flex items-center justify-between p-3 bg-purple-50 rounded-lg">
              <span className="text-sm font-medium text-gray-700">Stripe</span>
              <div className="flex items-center gap-2">
                <span className="text-2xl font-bold text-purple-900">
                  {analytics.paymentMethodBreakdown.stripe}
                </span>
                <span className="text-xs text-gray-600">
                  (
                  {analytics.totalOrders > 0
                    ? (
                        (analytics.paymentMethodBreakdown.stripe /
                          analytics.totalOrders) *
                        100
                      ).toFixed(0)
                    : 0}
                  %)
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Popular Items & Peak Hours */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
        {/* Popular Menu Items */}
        <div className="bg-white rounded-lg shadow p-6">
          <h2 className="text-lg font-bold text-gray-900 mb-4">
            Popular Menu Items
          </h2>
          {analytics.populateMenuItems.length > 0 ? (
            <div className="space-y-3">
              {analytics.populateMenuItems.slice(0, 5).map((item, idx) => (
                <div key={idx} className="p-3 bg-gray-50 rounded-lg">
                  <div className="flex items-center justify-between mb-2">
                    <span className="font-medium text-gray-900">
                      {idx + 1}. {item.name}
                    </span>
                    <span className="text-sm font-bold text-amber-600">
                      {item.count}x
                    </span>
                  </div>
                  <div className="w-full bg-gray-200 rounded-full h-2">
                    <div
                      className="bg-amber-600 h-2 rounded-full"
                      style={{
                        width: `${
                          (item.count /
                            Math.max(
                              ...analytics.populateMenuItems.map((i) => i.count)
                            )) *
                          100
                        }%`,
                      }}
                    ></div>
                  </div>
                  <p className="text-xs text-gray-600 mt-1">
                    Revenue: ${item.revenue.toFixed(2)}
                  </p>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-gray-600 text-center py-4">No data available</p>
          )}
        </div>

        {/* Peak Order Hours */}
        <div className="bg-white rounded-lg shadow p-6">
          <h2 className="text-lg font-bold text-gray-900 mb-4">
            Peak Order Times
          </h2>
          {analytics.peakHours.length > 0 ? (
            <div className="space-y-3">
              {analytics.peakHours
                .sort((a, b) => b.count - a.count)
                .slice(0, 5)
                .map((hour, idx) => (
                  <div key={idx} className="p-3 bg-gray-50 rounded-lg">
                    <div className="flex items-center justify-between mb-2">
                      <span className="font-medium text-gray-900">
                        {String(hour.hour).padStart(2, "0")}:00 -{" "}
                        {String(hour.hour + 1).padStart(2, "0")}:00
                      </span>
                      <span className="text-sm font-bold text-blue-600">
                        {hour.count} orders
                      </span>
                    </div>
                    <div className="w-full bg-gray-200 rounded-full h-2">
                      <div
                        className="bg-blue-600 h-2 rounded-full"
                        style={{
                          width: `${
                            (hour.count /
                              Math.max(
                                ...analytics.peakHours.map((h) => h.count)
                              )) *
                            100
                          }%`,
                        }}
                      ></div>
                    </div>
                  </div>
                ))}
            </div>
          ) : (
            <p className="text-gray-600 text-center py-4">No data available</p>
          )}
        </div>
      </div>

      {/* Revenue Trend */}
      <div className="bg-white rounded-lg shadow p-6 mb-8">
        <h2 className="text-lg font-bold text-gray-900 mb-4">Revenue Trend</h2>
        {analytics.revenueByDay.length > 0 ? (
          <div className="space-y-3">
            {analytics.revenueByDay.map((day, idx) => (
              <div key={idx} className="p-3 bg-gray-50 rounded-lg">
                <div className="flex items-center justify-between mb-2">
                  <span className="font-medium text-gray-900">{day.date}</span>
                  <div className="text-right">
                    <p className="font-bold text-green-600">
                      ${day.revenue.toFixed(2)}
                    </p>
                    <p className="text-xs text-gray-600">{day.orders} orders</p>
                  </div>
                </div>
                <div className="w-full bg-gray-200 rounded-full h-2">
                  <div
                    className="bg-green-600 h-2 rounded-full"
                    style={{
                      width: `${
                        (day.revenue /
                          Math.max(
                            ...analytics.revenueByDay.map((d) => d.revenue)
                          )) *
                        100
                      }%`,
                    }}
                  ></div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-gray-600 text-center py-4">No data available</p>
        )}
      </div>

      {/* Top Customers */}
      <div className="bg-white rounded-lg shadow p-6">
        <h2 className="text-lg font-bold text-gray-900 mb-4">Top Customers</h2>
        {analytics.topCustomers.length > 0 ? (
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-gray-200">
                  <th className="text-left py-3 px-4 font-semibold text-gray-700">
                    Customer
                  </th>
                  <th className="text-left py-3 px-4 font-semibold text-gray-700">
                    Orders
                  </th>
                  <th className="text-left py-3 px-4 font-semibold text-gray-700">
                    Total Spent
                  </th>
                  <th className="text-left py-3 px-4 font-semibold text-gray-700">
                    Avg Order
                  </th>
                </tr>
              </thead>
              <tbody>
                {analytics.topCustomers.slice(0, 10).map((customer, idx) => (
                  <tr
                    key={idx}
                    className="border-b border-gray-100 hover:bg-gray-50 transition"
                  >
                    <td className="py-3 px-4">
                      <div>
                        <p className="font-medium text-gray-900">
                          {customer.name}
                        </p>
                        <p className="text-xs text-gray-600">
                          {customer.email}
                        </p>
                      </div>
                    </td>
                    <td className="py-3 px-4">
                      <span className="font-semibold text-gray-900">
                        {customer.orderCount}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      <span className="font-semibold text-green-600">
                        ${customer.totalSpent.toFixed(2)}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      <span className="text-gray-600">
                        $
                        {(customer.totalSpent / customer.orderCount).toFixed(2)}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <p className="text-gray-600 text-center py-4">No customer data</p>
        )}
      </div>
    </div>
  );
}
