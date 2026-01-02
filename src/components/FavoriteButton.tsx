"use client";

import { useFavorites } from "@/hooks/useFavorites";

interface FavoriteButtonProps {
  menuItemId: string;
  name: string;
  price: number;
  description?: string;
  category?: string;
  className?: string;
  showLabel?: boolean;
}

export function FavoriteButton({
  menuItemId,
  name,
  price,
  description,
  category,
  className = "",
  showLabel = false,
}: FavoriteButtonProps) {
  const { isFavorite, toggleFavorite } = useFavorites();
  const isCurrentlyFavorite = isFavorite(menuItemId);

  const handleClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();

    toggleFavorite({
      menuItemId,
      name,
      price,
      description,
      category,
    });
  };

  return (
    <button
      onClick={handleClick}
      className={`flex items-center gap-2 transition ${className}`}
      title={isCurrentlyFavorite ? "Remove from favorites" : "Add to favorites"}
    >
      <svg
        className={`w-5 h-5 ${
          isCurrentlyFavorite
            ? "fill-red-500 text-red-500"
            : "text-gray-400 hover:text-red-500"
        }`}
        fill={isCurrentlyFavorite ? "currentColor" : "none"}
        stroke="currentColor"
        viewBox="0 0 24 24"
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          strokeWidth={isCurrentlyFavorite ? 0 : 2}
          d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"
        />
      </svg>
      {showLabel && (
        <span className="text-xs font-medium">
          {isCurrentlyFavorite ? "Favorited" : "Add to favorites"}
        </span>
      )}
    </button>
  );
}
