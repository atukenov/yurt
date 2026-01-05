"use client";

import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";

interface ReviewItem {
  _id: string;
  menuItem: {
    _id: string;
    name: string;
  };
  customer: {
    _id: string;
    name: string;
    email: string;
  };
  rating: number;
  comment?: string;
  createdAt: string;
  isApproved: boolean;
}

export default function AdminReviewsPage() {
  const router = useRouter();
  const { data: session, status } = useSession();

  const [reviews, setReviews] = useState<ReviewItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<"all" | "pending" | "approved">(
    "pending"
  );

  useEffect(() => {
    // Wait for session to load
    if (status === "loading") {
      return;
    }

    // Check if user is authenticated
    if (status === "unauthenticated") {
      router.push("/login");
      return;
    }

    // Check if user is admin
    if (session?.user?.role !== "admin") {
      router.push("/");
      return;
    }

    fetchReviews();
  }, [status, session, router, filter]);

  const fetchReviews = async () => {
    try {
      setLoading(true);
      const approved =
        filter === "all" ? undefined : filter === "approved" ? "true" : "false";
      const url = `/api/reviews${approved ? `?approved=${approved}` : ""}`;

      const res = await fetch(url);
      if (res.ok) {
        const data = await res.json();
        setReviews(data.reviews || []);
      }
    } catch (error) {
      console.error("Error fetching reviews:", error);
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (reviewId: string) => {
    try {
      const res = await fetch(`/api/reviews/${reviewId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ isApproved: true }),
      });

      if (res.ok) {
        fetchReviews();
      }
    } catch (error) {
      console.error("Error approving review:", error);
    }
  };

  const handleReject = async (reviewId: string) => {
    try {
      const res = await fetch(`/api/reviews/${reviewId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ isApproved: false }),
      });

      if (res.ok) {
        fetchReviews();
      }
    } catch (error) {
      console.error("Error rejecting review:", error);
    }
  };

  const handleDelete = async (reviewId: string) => {
    if (!confirm("Delete this review?")) return;

    try {
      const res = await fetch(`/api/reviews/${reviewId}`, {
        method: "DELETE",
      });

      if (res.ok) {
        fetchReviews();
      }
    } catch (error) {
      console.error("Error deleting review:", error);
    }
  };

  // Show loading state while checking authentication
  if (status === "loading" || session?.user?.role !== "admin") {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <p className="text-gray-600">Loading...</p>
      </div>
    );
  }

  return (
    <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900 mb-6">
          Review Moderation
        </h1>

        {/* Filter Tabs */}
        <div className="flex gap-4 mb-6">
          {["all", "pending", "approved"].map((tab) => (
            <button
              key={tab}
              onClick={() => setFilter(tab as "all" | "pending" | "approved")}
              className={`px-4 py-2 rounded-lg font-semibold transition ${
                filter === tab
                  ? "bg-[#ffd119] text-black"
                  : "bg-gray-200 text-gray-700 hover:bg-gray-300"
              }`}
            >
              {tab.charAt(0).toUpperCase() + tab.slice(1)}
              {tab === "pending" &&
                ` (${reviews.filter((r) => !r.isApproved).length})`}
              {tab === "approved" &&
                ` (${reviews.filter((r) => r.isApproved).length})`}
              {tab === "all" && ` (${reviews.length})`}
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <div className="text-center py-12">
          <p className="text-gray-600">Loading reviews...</p>
        </div>
      ) : reviews.length === 0 ? (
        <div className="bg-white rounded-lg shadow p-8 text-center">
          <p className="text-gray-600">No reviews to display</p>
        </div>
      ) : (
        <div className="space-y-4">
          {reviews.map((review) => (
            <div
              key={review._id}
              className={`bg-white rounded-lg shadow p-6 border-l-4 ${
                review.isApproved ? "border-l-green-500" : "border-l-yellow-500"
              }`}
            >
              <div className="flex justify-between items-start mb-4">
                <div>
                  <h3 className="font-bold text-lg text-gray-900">
                    {review.menuItem.name}
                  </h3>
                  <p className="text-sm text-gray-600">
                    By {review.customer.name} ({review.customer.email})
                  </p>
                  <p className="text-xs text-gray-500">
                    {new Date(review.createdAt).toLocaleString()}
                  </p>
                </div>

                <div className="flex gap-1">
                  {[1, 2, 3, 4, 5].map((star) => (
                    <span
                      key={star}
                      className={
                        star <= review.rating
                          ? "text-yellow-400"
                          : "text-gray-300"
                      }
                    >
                      ★
                    </span>
                  ))}
                </div>
              </div>

              {review.comment && (
                <p className="text-gray-700 mb-4 bg-gray-50 p-3 rounded">
                  "{review.comment}"
                </p>
              )}

              <div className="flex justify-between items-center">
                <div>
                  {review.isApproved ? (
                    <span className="inline-block px-3 py-1 bg-green-100 text-green-700 rounded-full text-xs font-semibold">
                      ✓ Approved
                    </span>
                  ) : (
                    <span className="inline-block px-3 py-1 bg-yellow-100 text-yellow-700 rounded-full text-xs font-semibold">
                      ⏳ Pending
                    </span>
                  )}
                </div>

                <div className="flex gap-2">
                  {!review.isApproved && (
                    <button
                      onClick={() => handleApprove(review._id)}
                      className="px-4 py-2 bg-green-100 text-green-700 rounded-lg hover:bg-green-200 transition font-semibold text-sm"
                    >
                      Approve
                    </button>
                  )}
                  {review.isApproved && (
                    <button
                      onClick={() => handleReject(review._id)}
                      className="px-4 py-2 bg-yellow-100 text-yellow-700 rounded-lg hover:bg-yellow-200 transition font-semibold text-sm"
                    >
                      Unapprove
                    </button>
                  )}
                  <button
                    onClick={() => handleDelete(review._id)}
                    className="px-4 py-2 bg-red-100 text-red-700 rounded-lg hover:bg-red-200 transition font-semibold text-sm"
                  >
                    Delete
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
