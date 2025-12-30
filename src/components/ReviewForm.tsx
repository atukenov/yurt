"use client";

import { IMenuItem } from "@/types";
import { useSession } from "next-auth/react";
import { useState } from "react";

interface ReviewFormProps {
  orderId: string;
  menuItem: IMenuItem;
  onSubmit: () => void;
  onCancel?: () => void;
}

export function ReviewForm({
  orderId,
  menuItem,
  onSubmit,
  onCancel,
}: ReviewFormProps) {
  const { data: session } = useSession();
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (rating === 0) {
      setError("Please select a rating");
      return;
    }

    try {
      setLoading(true);
      setError("");

      const res = await fetch("/api/reviews", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          // Use session user ID as bearer token for now
          Authorization: `Bearer ${session?.user?.id || ""}`,
        },
        body: JSON.stringify({
          orderId,
          menuItemId: menuItem._id,
          rating,
          comment: comment.trim() || undefined,
        }),
      });

      if (res.ok) {
        setSuccess(true);
        setRating(0);
        setComment("");
        setTimeout(() => {
          onSubmit();
        }, 1500);
      } else {
        const data = await res.json();
        setError(data.error || "Failed to submit review");
      }
    } catch (err) {
      setError("Error submitting review");
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  if (success) {
    return (
      <div className="bg-green-50 border border-green-200 rounded-lg p-4 text-center">
        <p className="text-green-700 font-semibold">✅ Review submitted!</p>
        <p className="text-sm text-green-600">Thank you for your feedback</p>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div>
        <label className="block text-sm font-medium text-gray-700 mb-3">
          Rating for {menuItem.name}
        </label>
        <div className="flex gap-2">
          {[1, 2, 3, 4, 5].map((star) => (
            <button
              key={star}
              type="button"
              onClick={() => setRating(star)}
              className={`text-3xl transition-transform hover:scale-110 ${
                rating >= star ? "text-yellow-400" : "text-gray-300"
              }`}
              aria-label={`Rate ${star} stars`}
            >
              ★
            </button>
          ))}
        </div>
        {rating > 0 && (
          <p className="text-sm text-gray-600 mt-2">
            {rating === 1 && "Poor"}
            {rating === 2 && "Fair"}
            {rating === 3 && "Good"}
            {rating === 4 && "Very Good"}
            {rating === 5 && "Excellent"}
          </p>
        )}
      </div>

      <div>
        <label className="block text-sm font-medium text-gray-700 mb-2">
          Comment (optional)
        </label>
        <textarea
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          placeholder="Share your experience..."
          maxLength={500}
          className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-amber-500 resize-none"
          rows={3}
        />
        <p className="text-xs text-gray-500 mt-1">
          {comment.length}/500 characters
        </p>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-lg p-3">
          <p className="text-red-700 text-sm">{error}</p>
        </div>
      )}

      <div className="flex gap-2">
        <button
          type="submit"
          disabled={loading || rating === 0}
          className="flex-1 px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 disabled:opacity-50 disabled:cursor-not-allowed font-semibold transition"
        >
          {loading ? "Submitting..." : "Submit Review"}
        </button>
        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            className="px-4 py-2 bg-gray-200 text-gray-700 rounded-lg hover:bg-gray-300 font-semibold transition"
          >
            Cancel
          </button>
        )}
      </div>
    </form>
  );
}
