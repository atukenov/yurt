"use client";

import { useLanguage } from "@/context/LanguageContext";
import { translations, type Language } from "@/lib/translations";

export interface DisplayReview {
  _id: string;
  menuItem: {
    _id: string;
    name: string;
  };
  customer: {
    name: string;
  };
  rating: number;
  comment?: string;
  createdAt: string;
  isApproved?: boolean;
}

interface ReviewDisplayProps {
  reviews: DisplayReview[];
  showApprovalStatus?: boolean;
}

export function ReviewDisplay({
  reviews,
  showApprovalStatus = false,
}: ReviewDisplayProps) {
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

  if (reviews.length === 0) {
    return (
      <div className="bg-gray-50 rounded-lg p-6 text-center">
        <p className="text-gray-600">{t.noReviewsYet}</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {reviews.map((review) => (
        <div
          key={review._id}
          className="bg-white border border-gray-200 rounded-lg p-4"
        >
          <div className="flex justify-between items-start mb-2">
            <div>
              <p className="font-semibold text-gray-900">
                {review.customer.name}
              </p>
              <p className="text-xs text-gray-600">
                {review.menuItem.name} •{" "}
                {new Date(review.createdAt).toLocaleDateString()}
              </p>
            </div>
            <div className="flex items-center gap-1">
              {[1, 2, 3, 4, 5].map((star) => (
                <span
                  key={star}
                  className={
                    star <= review.rating ? "text-yellow-400" : "text-gray-300"
                  }
                >
                  ★
                </span>
              ))}
            </div>
          </div>

          {review.comment && (
            <p className="text-gray-700 mb-2">{review.comment}</p>
          )}

          {showApprovalStatus && (
            <div className="mt-2 pt-2 border-t border-gray-200">
              {review.isApproved ? (
                <p className="text-xs text-green-600 flex items-center gap-1">
                  ✓ {t.approved}
                </p>
              ) : (
                <p className="text-xs text-yellow-600 flex items-center gap-1">
                  ⏳ {t.pendingApproval}
                </p>
              )}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

interface RatingStatsProps {
  ratings: number[];
  menuItemName?: string;
}

export function RatingStats({ ratings, menuItemName }: RatingStatsProps) {
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

  if (ratings.length === 0) {
    return (
      <div className="text-center py-4">
        <p className="text-gray-500">{t.noRatingsYet}</p>
      </div>
    );
  }

  const average = ratings.reduce((a, b) => a + b, 0) / ratings.length;
  const counts = [0, 0, 0, 0, 0];

  ratings.forEach((rating) => {
    if (rating >= 1 && rating <= 5) {
      counts[rating - 1]++;
    }
  });

  return (
    <div className="bg-white rounded-lg p-4 border border-gray-200">
      {menuItemName && (
        <h3 className="font-semibold text-gray-900 mb-3">{menuItemName}</h3>
      )}

      <div className="flex items-center gap-3 mb-4">
        <div>
          <p className="text-3xl font-bold text-amber-600">
            {average.toFixed(1)}
          </p>
          <div className="flex gap-1">
            {[1, 2, 3, 4, 5].map((star) => (
              <span
                key={star}
                className={
                  star <= Math.round(average)
                    ? "text-yellow-400"
                    : "text-gray-300"
                }
              >
                ★
              </span>
            ))}
          </div>
        </div>
        <p className="text-sm text-gray-600">
          Based on {ratings.length}{" "}
          {ratings.length === 1 ? "review" : "reviews"}
        </p>
      </div>

      <div className="space-y-2">
        {[5, 4, 3, 2, 1].map((rating) => {
          const count = counts[rating - 1];
          const percentage =
            ratings.length > 0 ? Math.round((count / ratings.length) * 100) : 0;

          return (
            <div key={rating} className="flex items-center gap-2">
              <span className="text-sm text-gray-600 w-8">{rating}★</span>
              <div className="flex-1 bg-gray-200 rounded-full h-2">
                <div
                  className="bg-yellow-400 h-2 rounded-full transition-all"
                  style={{ width: `${percentage}%` }}
                />
              </div>
              <span className="text-sm text-gray-600 w-8 text-right">
                {count}
              </span>
            </div>
          );
        })}
      </div>
    </div>
  );
}
