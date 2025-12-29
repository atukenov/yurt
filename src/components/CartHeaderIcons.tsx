"use client";

import type { CartItem } from "@/store/cart";
import { useCartStore } from "@/store/cart";
import Link from "next/link";

interface CartHeaderIconsProps {
  showSearch: boolean;
  setShowSearch: (show: boolean) => void;
  setSearchQuery: (query: string) => void;
  searchInputRef: React.RefObject<HTMLInputElement | null>;
}

export function CartHeaderIcons({
  showSearch,
  setShowSearch,
  setSearchQuery,
  searchInputRef,
}: CartHeaderIconsProps) {
  const cartItems = useCartStore((state) => state.items) as CartItem[];
  const cartCount = cartItems.reduce(
    (sum: number, item: CartItem) => sum + item.quantity,
    0
  );

  return (
    <div className="flex items-center gap-4">
      {/* Search Button */}
      <button
        onClick={() => {
          if (showSearch) setSearchQuery("");
          setShowSearch(!showSearch);
        }}
        className="p-2 text-gray-700 hover:text-amber-600 transition shrink-0"
        title="Search menu"
      >
        <svg
          className="w-6 h-6"
          fill="none"
          stroke="currentColor"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            strokeWidth={2}
            d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
          />
        </svg>
      </button>

      {/* Favorites */}
      <button
        className="p-2 text-gray-700 hover:text-amber-600 transition shrink-0"
        title="Favorites"
      >
        <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
        </svg>
      </button>

      {/* Cart Icon with badge */}
      <Link
        href="/cart"
        className="relative p-2 text-gray-700 hover:text-amber-600 transition shrink-0"
        title="Shopping cart"
      >
        <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
          <path d="M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-0.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49A1.003 1.003 0 0 0 20 4H5.21l-.94-2H1zm16 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-0.9-2-2-2z" />
        </svg>
        {cartCount > 0 && (
          <span className="absolute -top-1 -right-1 bg-amber-600 text-white text-xs font-bold rounded-full px-1.5 py-0.5 min-w-6 text-center">
            {cartCount}
          </span>
        )}
      </Link>
    </div>
  );
}
