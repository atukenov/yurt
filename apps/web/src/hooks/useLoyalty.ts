"use client";

import { apiClient } from "@/lib/api-client";
import { useCallback, useEffect, useState } from "react";

export interface LoyaltyStatus {
  totalPoints: number;
  availablePoints: number;
  tier: string;
  totalSpent: number;
  orderCount: number;
  lastOrderDate?: string;
  tierBenefits: {
    tier: string;
    pointsPerDollar: number;
    redemptionRate: number;
    nextTier?: string;
    pointsUntilNextTier: number;
  };
}

export interface AwardPointsResponse {
  pointsEarned: number;
  totalPoints: number;
  availablePoints: number;
  tier: string;
  tierBenefits: LoyaltyStatus["tierBenefits"];
}

export function useLoyalty() {
  const [status, setStatus] = useState<LoyaltyStatus | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Fetch loyalty status
  const fetchStatus = useCallback(async () => {
    try {
      setIsLoading(true);
      const data = await apiClient.get("/loyalty/status");
      setStatus(data.data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unknown error");
      console.error("[Loyalty] Error fetching status:", err);
    } finally {
      setIsLoading(false);
    }
  }, []);

  // Load status on mount
  useEffect(() => {
    fetchStatus();
  }, [fetchStatus]);

  // Award points for order
  const awardPoints = useCallback(
    async (orderId: string, orderAmount: number) => {
      try {
        const data: { data: AwardPointsResponse } = await apiClient.post(
          "/loyalty/award",
          { orderId, orderAmount }
        );

        // Update local status
        setStatus((prev) =>
          prev
            ? {
                ...prev,
                totalPoints: data.data.totalPoints,
                availablePoints: data.data.availablePoints,
                tier: data.data.tier,
                tierBenefits: data.data.tierBenefits,
              }
            : null
        );

        return data.data;
      } catch (err) {
        const message = err instanceof Error ? err.message : "Unknown error";
        setError(message);
        throw err;
      }
    },
    []
  );

  // Redeem points
  const redeemPoints = useCallback(async (points: number, orderId: string) => {
    try {
      const data: {
        data: {
          discount: number;
          pointsRedeemed: number;
          remainingPoints: number;
        };
      } = await apiClient.post("/loyalty/redeem", { points, orderId });

      // Update local status
      setStatus((prev) =>
        prev
          ? {
              ...prev,
              availablePoints: data.data.remainingPoints,
            }
          : null
      );

      return data.data;
    } catch (err) {
      const message =
        (err as any).data?.error ||
        (err instanceof Error ? err.message : "Unknown error");
      setError(message);
      throw err;
    }
  }, []);

  // Get discount value for points
  const getDiscountForPoints = useCallback(
    (points: number) => {
      return points / (status?.tierBenefits.redemptionRate || 100);
    },
    [status]
  );

  // Get points needed for next tier
  const getPointsToNextTier = useCallback(() => {
    return status?.tierBenefits.pointsUntilNextTier || 0;
  }, [status]);

  return {
    status,
    isLoading,
    error,
    refreshStatus: fetchStatus,
    awardPoints,
    redeemPoints,
    getDiscountForPoints,
    getPointsToNextTier,
  };
}
