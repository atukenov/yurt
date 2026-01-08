"use client";

import { useLanguage } from "@/context/LanguageContext";
import { useLoyalty } from "@/hooks/useLoyalty";
import { translations } from "@/lib/translations";

const TIER_COLORS = {
  bronze: "from-amber-700 to-amber-600",
  silver: "from-gray-400 to-gray-300",
  gold: "from-yellow-500 to-yellow-400",
  platinum: "from-blue-600 to-blue-500",
};

const TIER_ICONS = {
  bronze: "🥉",
  silver: "🥈",
  gold: "🥇",
  platinum: "👑",
};

export function LoyaltyProfileCard() {
  const { status, isLoading, error } = useLoyalty();

  // Safely access language context with fallback
  let language: "en" | "ru" = "en";
  let t = translations.en.client;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.client || translations.en.client;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.client;
  }

  if (isLoading) {
    return (
      <div className="bg-white rounded-lg shadow p-6">
        <div className="animate-pulse space-y-4">
          <div className="h-32 bg-gray-200 rounded"></div>
          <div className="h-4 bg-gray-200 rounded w-3/4"></div>
          <div className="h-4 bg-gray-200 rounded w-1/2"></div>
        </div>
      </div>
    );
  }

  if (error || !status) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-lg p-4">
        <p className="text-red-700">{t.failedLoadLoyalty}</p>
      </div>
    );
  }

  const tier = status.tier.toLowerCase();
  const tierColor =
    TIER_COLORS[tier as keyof typeof TIER_COLORS] || TIER_COLORS.bronze;
  const tierIcon =
    TIER_ICONS[tier as keyof typeof TIER_ICONS] || TIER_ICONS.bronze;

  return (
    <div className="space-y-6">
      {/* Tier Card */}
      <div
        className={`bg-gradient-to-r ${tierColor} rounded-lg shadow-lg p-6 text-white`}
      >
        <div className="flex items-center justify-between mb-4">
          <div>
            <p className="text-white/80 text-sm font-medium">Your Tier</p>
            <h2 className="text-3xl font-bold capitalize flex items-center gap-2">
              <span>{tierIcon}</span>
              {status.tier}
            </h2>
          </div>
          <div className="text-right">
            <p className="text-white/80 text-sm">Total Spent</p>
            <p className="text-2xl font-bold">
              ${status.totalSpent.toFixed(2)}
            </p>
          </div>
        </div>

        {/* Points Progress to Next Tier */}
        {status.tierBenefits.nextTier && (
          <div>
            <div className="flex justify-between items-center mb-2">
              <span className="text-sm text-white/80">
                To {status.tierBenefits.nextTier.toUpperCase()}
              </span>
              <span className="text-sm font-semibold">
                {status.tierBenefits.pointsUntilNextTier} points needed
              </span>
            </div>
            <div className="w-full bg-white/20 rounded-full h-2">
              <div
                className="bg-white rounded-full h-2 transition-all duration-300"
                style={{
                  width: `${Math.min(
                    100,
                    ((status.totalPoints -
                      (status.totalPoints -
                        status.tierBenefits.pointsUntilNextTier)) /
                      status.tierBenefits.pointsUntilNextTier) *
                      100
                  )}%`,
                }}
              ></div>
            </div>
          </div>
        )}
      </div>

      {/* Points Summary */}
      <div className="grid grid-cols-2 gap-4">
        {/* Available Points */}
        <div className="bg-gradient-to-br from-green-50 to-green-100 rounded-lg p-4 border border-green-200">
          <p className="text-green-700 text-sm font-medium">Available Points</p>
          <p className="text-2xl font-bold text-green-900 mt-1">
            {status.availablePoints}
          </p>
          <p className="text-xs text-green-600 mt-2">
            ${(status.availablePoints / 100).toFixed(2)} in rewards
          </p>
        </div>

        {/* Total Points */}
        <div className="bg-gradient-to-br from-blue-50 to-blue-100 rounded-lg p-4 border border-blue-200">
          <p className="text-blue-700 text-sm font-medium">
            Total Points Earned
          </p>
          <p className="text-2xl font-bold text-blue-900 mt-1">
            {status.totalPoints}
          </p>
          <p className="text-xs text-blue-600 mt-2">
            From {status.orderCount} order{status.orderCount !== 1 ? "s" : ""}
          </p>
        </div>
      </div>

      {/* Tier Benefits */}
      <div className="bg-gray-50 rounded-lg p-4 border border-gray-200">
        <h3 className="font-semibold text-gray-900 mb-3">Your Benefits</h3>
        <div className="space-y-2">
          <div className="flex justify-between items-center">
            <span className="text-gray-700">Points per $1 spent</span>
            <span className="font-semibold text-amber-600">
              {status.tierBenefits.pointsPerDollar}x
            </span>
          </div>
          <div className="flex justify-between items-center">
            <span className="text-gray-700">Redemption value</span>
            <span className="font-semibold text-green-600">
              100 points = $1
            </span>
          </div>
          {status.lastOrderDate && (
            <div className="flex justify-between items-center pt-2 border-t border-gray-200 mt-2">
              <span className="text-gray-700 text-sm">Last order</span>
              <span className="text-gray-600 text-sm">
                {new Date(status.lastOrderDate).toLocaleDateString()}
              </span>
            </div>
          )}
        </div>
      </div>

      {/* How Points Work */}
      <div className="bg-blue-50 rounded-lg p-4 border border-blue-200">
        <h3 className="font-semibold text-gray-900 mb-2 flex items-center gap-2">
          <span>💡</span> How Points Work
        </h3>
        <ul className="space-y-1 text-sm text-gray-700">
          <li>
            ✓ Earn {status.tierBenefits.pointsPerDollar}x points per dollar
            spent
          </li>
          <li>✓ Redeem 100 points for $1 off your order</li>
          <li>✓ Higher tiers unlock better rewards</li>
          <li>✓ Birthday bonus: 100 bonus points</li>
        </ul>
      </div>
    </div>
  );
}
