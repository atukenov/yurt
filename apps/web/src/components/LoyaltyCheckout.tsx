"use client";

import { useToastNotification } from "@/components/ToastProvider";
import { useLanguage } from "@/context/LanguageContext";
import { useLoyalty } from "@/hooks/useLoyalty";
import { translations, type Language } from "@/lib/translations";
import { useState } from "react";

interface LoyaltyCheckoutProps {
  onPointsRedeemed?: (discount: number) => void;
}

export function LoyaltyCheckout({ onPointsRedeemed }: LoyaltyCheckoutProps) {
  const { status, redeemPoints } = useLoyalty();
  const { showToast } = useToastNotification();
  const [pointsToRedeem, setPointsToRedeem] = useState(0);
  const [isRedeeming, setIsRedeeming] = useState(false);
  const [maxPointsApplied, setMaxPointsApplied] = useState(false);

  // Safely access language context with fallback
  let language: Language = "en";
  let t = translations.en.client;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.client || translations.en.client;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.client;
  }

  if (!status || status.availablePoints === 0) {
    return null;
  }

  const maxRedeemable = Math.min(
    status.availablePoints,
    Math.floor((100 * status.availablePoints) / 100) // Max 100 points per $1
  );
  const potentialDiscount = pointsToRedeem / 100;

  const handleMaxPoints = () => {
    setPointsToRedeem(maxRedeemable);
    setMaxPointsApplied(true);
  };

  const handleRedeem = async () => {
    if (pointsToRedeem <= 0) {
      showToast(t.pleaseSelectPoints, "error");
      return;
    }

    if (pointsToRedeem > status.availablePoints) {
      showToast(t.notEnoughPoints, "error");
      return;
    }

    setIsRedeeming(true);
    try {
      const result = await redeemPoints(pointsToRedeem, "pending");
      showToast(
        `${language === "ru" ? "Обменено" : "Redeemed"} ${pointsToRedeem} ${
          t.points
        } ${t.for} ₸${result.discount.toFixed(0)} ${t.off}!`,
        "success"
      );
      onPointsRedeemed?.(result.discount);
      setPointsToRedeem(0);
      setMaxPointsApplied(false);
    } catch (error) {
      showToast(
        error instanceof Error ? error.message : t.failedRedeem,
        "error"
      );
    } finally {
      setIsRedeeming(false);
    }
  };

  return (
    <div className="bg-gradient-to-r from-amber-50 to-yellow-50 rounded-lg p-4 border border-amber-200">
      <div className="flex items-center justify-between mb-3">
        <h3 className="font-semibold text-gray-900 flex items-center gap-2">
          <span>🎁</span> {t.redeemLoyaltyPoints}
        </h3>
        <span className="text-sm text-gray-600">
          {t.availablePoints}{" "}
          <span className="font-semibold">{status.availablePoints}</span>
        </span>
      </div>

      <div className="space-y-3">
        {/* Points Slider */}
        <div>
          <div className="flex justify-between items-center mb-2">
            <label className="text-sm font-medium text-gray-700">
              {t.points} {t.redeem}
            </label>
            <button
              onClick={handleMaxPoints}
              className="text-xs px-2 py-1 bg-white border border-amber-300 text-amber-600 rounded hover:bg-amber-50 transition"
            >
              {t.max}
            </button>
          </div>
          <input
            type="range"
            min="0"
            max={maxRedeemable}
            step="10"
            value={pointsToRedeem}
            onChange={(e) => {
              setPointsToRedeem(parseInt(e.target.value));
              setMaxPointsApplied(false);
            }}
            className="w-full h-2 bg-gray-300 rounded-lg appearance-none cursor-pointer accent-amber-600"
          />
          <div className="flex justify-between items-center mt-2">
            <span className="text-sm text-gray-600">
              {pointsToRedeem} / {maxRedeemable}
            </span>
            <span className="text-sm font-semibold text-green-600">
              = ₸{potentialDiscount.toFixed(0)} {t.off}
            </span>
          </div>
        </div>

        {/* Discount Preview */}
        {pointsToRedeem > 0 && (
          <div className="bg-white rounded p-2 border border-green-200">
            <p className="text-xs text-gray-600">
              {language === "ru" ? "Вы сэкономите" : "You'll save"}{" "}
              <span className="font-bold text-green-600">
                {potentialDiscount.toFixed(0)} ₸
              </span>{" "}
              {language === "ru" ? "на этом заказе" : "on this order"}
            </p>
          </div>
        )}

        {/* Redeem Button */}
        {pointsToRedeem > 0 && (
          <button
            onClick={handleRedeem}
            disabled={isRedeeming}
            className="w-full py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 disabled:bg-gray-400 disabled:cursor-not-allowed transition font-medium text-sm"
          >
            {isRedeeming
              ? t.processing
              : `${t.redeem} ${pointsToRedeem} ${t.points}`}
          </button>
        )}
      </div>

      {/* Info */}
      <p className="text-xs text-gray-600 mt-3 italic">
        💡{" "}
        {language === "ru"
          ? "100 баллов = 1 ₸ скидка. Обменяйте баллы на скидки на ваш заказ!"
          : "100 points = 1 ₸ off. Redeem points to get discounts on your order!"}
      </p>
    </div>
  );
}
