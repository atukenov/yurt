"use client";

import { useCartStore } from "@/store/cart";
import Link from "next/link";

// Cart icon and badge for header (must be outside component body)
import type { CartItem } from "@/store/cart";
type CartHeaderIconsProps = {
  showSearch: boolean;
  setShowSearch: (show: boolean) => void;
  setSearchQuery: (query: string) => void;
};

function CartHeaderIcons({
  showSearch,
  setShowSearch,
  setSearchQuery,
}: CartHeaderIconsProps) {
  const cartItems = useCartStore((state) => state.items) as CartItem[];
  const cartCount = cartItems.reduce(
    (sum: number, item: CartItem) => sum + item.quantity,
    0
  );
  return (
    <div className="flex items-center gap-4 relative">
      {/* Search Button */}
      <button
        onClick={() => {
          if (showSearch) setSearchQuery("");
          setShowSearch(!showSearch);
        }}
        className="p-2 text-gray-700 hover:text-amber-600 transition shrink-0"
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
      <button className="p-2 text-gray-700 hover:text-amber-600 transition shrink-0">
        <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
          <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z" />
        </svg>
      </button>
      {/* Cart Icon with badge */}
      <Link
        href="/cart"
        className="relative p-2 text-gray-700 hover:text-amber-600 transition shrink-0"
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

import { signOut, useSession } from "next-auth/react";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { IconContext } from "react-icons";
import { FaReceipt } from "react-icons/fa6";
import { IoLogOut } from "react-icons/io5";

export function Header() {
  const { data: session } = useSession();
  const router = useRouter();
  const pathname = usePathname();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [showSearch, setShowSearch] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedLocation, setSelectedLocation] = useState("All Locations");
  const searchInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (showSearch && searchInputRef.current) {
      searchInputRef.current.focus();
    }
  }, [showSearch]);

  const handleSearchChange = (query: string) => {
    setSearchQuery(query);
    if (pathname === "/menu" && query) {
      router.push(`/menu?search=${encodeURIComponent(query)}`);
    }
  };

  const handleLocationChange = (location: string) => {
    setSelectedLocation(location);
    if (pathname === "/menu") {
      router.push(`/menu?location=${encodeURIComponent(location)}`);
    }
  };

  return (
    <>
      {/* Customer Header - Top Navigation */}
      {session?.user?.role === "customer" && (
        <header className="sticky top-0 z-40 bg-white border-b border-gray-200 shadow-sm">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex justify-between items-center h-16">
              {/* Left: Location Selector or Search Input */}
              <div className="flex items-center flex-1">
                {showSearch ? (
                  <input
                    ref={searchInputRef}
                    type="text"
                    placeholder="Search menu..."
                    value={searchQuery}
                    onChange={(e) => handleSearchChange(e.target.value)}
                    className="w-full px-4 py-2 border border-gray-300 rounded-lg text-gray-700 focus:outline-none focus:border-amber-600"
                  />
                ) : (
                  <select
                    value={selectedLocation}
                    onChange={(e) => handleLocationChange(e.target.value)}
                    className="px-4 py-2 border border-none rounded-lg text-gray-700 hover:border-amber-600 focus:outline-none focus:border-amber-600 transition"
                  >
                    <option>All Locations</option>
                    <option>Downtown</option>
                    <option>Uptown</option>
                    <option>Airport</option>
                    <option>Mall</option>
                  </select>
                )}
              </div>

              {/* Right: Favorites, Cart, Search */}
              <CartHeaderIcons
                showSearch={showSearch}
                setShowSearch={setShowSearch}
                setSearchQuery={setSearchQuery}
              />
            </div>
          </div>
        </header>
      )}

      {/* Admin & Auth Header - Original */}
      <header
        className={`sticky top-0 z-40 bg-white border-b border-gray-200 shadow-sm ${
          session?.user?.role === "customer" ? "hidden md:block" : ""
        }`}
      >
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center h-16">
            {/* Logo */}
            <Link href="/" className="flex items-center gap-2">
              <div className="w-8 h-8 bg-amber-600 rounded-full flex items-center justify-center">
                <span className="text-white font-bold text-lg">☕</span>
              </div>
              <span className="hidden sm:inline font-bold text-lg text-gray-900">
                Yurt Coffee
              </span>
            </Link>

            {/* Desktop Navigation */}
            <nav className="hidden md:flex items-center gap-6">
              {session?.user ? (
                <>
                  {session.user.role === "admin" ? (
                    <>
                      <Link
                        href="/admin"
                        className="text-gray-700 hover:text-amber-600 transition font-semibold"
                      >
                        Dashboard
                      </Link>
                      <Link
                        href="/admin/menu"
                        className="text-gray-700 hover:text-amber-600 transition"
                      >
                        Menu Management
                      </Link>
                      <Link
                        href="/admin/toppings"
                        className="text-gray-700 hover:text-amber-600 transition"
                      >
                        Toppings Management
                      </Link>
                      <Link
                        href="/admin/locations"
                        className="text-gray-700 hover:text-amber-600 transition"
                      >
                        Locations
                      </Link>
                    </>
                  ) : (
                    <>
                      <Link
                        href="/menu"
                        className="text-gray-700 hover:text-amber-600 transition"
                      >
                        Menu
                      </Link>
                      <Link
                        href="/orders"
                        className="text-gray-700 hover:text-amber-600 transition"
                      >
                        My Orders
                      </Link>
                    </>
                  )}

                  <div className="flex items-center gap-4 pl-4 border-l border-gray-200">
                    <span className="text-sm text-gray-600">
                      {session.user.email}
                    </span>
                    <button
                      onClick={() => signOut()}
                      className="text-gray-700 hover:text-red-600 transition text-sm"
                    >
                      <IconContext.Provider value={{ size: "2em" }}>
                        <IoLogOut />
                      </IconContext.Provider>
                    </button>
                  </div>
                </>
              ) : (
                <>
                  <Link
                    href="/login"
                    className="text-gray-700 hover:text-amber-600 transition"
                  >
                    Login
                  </Link>
                  <Link
                    href="/register"
                    className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 transition"
                  >
                    Register
                  </Link>
                </>
              )}
            </nav>

            {/* Mobile menu button */}
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="md:hidden p-2"
              aria-label="Toggle menu"
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
                  d="M4 6h16M4 12h16M4 18h16"
                />
              </svg>
            </button>
          </div>

          {/* Mobile Navigation */}
          {mobileMenuOpen && (
            <nav className="md:hidden pb-4 border-t border-gray-200">
              {session?.user ? (
                <>
                  {session.user.role === "admin" ? (
                    <>
                      <Link
                        href="/admin"
                        className="block py-2 text-gray-700 hover:text-amber-600 font-semibold"
                      >
                        Dashboard
                      </Link>
                      <Link
                        href="/admin/menu"
                        className="block py-2 text-gray-700 hover:text-amber-600"
                      >
                        Menu
                      </Link>
                      <Link
                        href="/admin/toppings"
                        className="block py-2 text-gray-700 hover:text-amber-600"
                      >
                        Toppings
                      </Link>
                      <Link
                        href="/admin/locations"
                        className="block py-2 text-gray-700 hover:text-amber-600"
                      >
                        Locations
                      </Link>
                    </>
                  ) : (
                    <>
                      <Link
                        href="/menu"
                        className="block py-2 text-gray-700 hover:text-amber-600"
                      >
                        Menu
                      </Link>
                      <Link
                        href="/orders"
                        className="block py-2 text-gray-700 hover:text-amber-600"
                      >
                        My Orders
                      </Link>
                    </>
                  )}
                  <hr className="my-2" />
                  <span className="block text-sm text-gray-600 py-2">
                    {session.user.email}
                  </span>
                  <button
                    onClick={() => signOut()}
                    className="block w-full text-left text-gray-700 hover:text-red-600 py-2"
                  >
                    <IconContext.Provider value={{ size: "1.5em" }}>
                      <IoLogOut />
                    </IconContext.Provider>
                  </button>
                </>
              ) : (
                <>
                  <Link
                    href="/login"
                    className="block py-2 text-gray-700 hover:text-amber-600"
                  >
                    Login
                  </Link>
                  <Link
                    href="/register"
                    className="block py-2 text-gray-700 hover:text-amber-600"
                  >
                    Register
                  </Link>
                </>
              )}
            </nav>
          )}
        </div>
      </header>

      {/* Mobile Bottom Navigation */}
      {session?.user && session.user.role === "customer" && (
        <nav className="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-white border-t border-gray-200 shadow-lg">
          <div className="flex justify-around items-center h-16">
            <Link
              href="/menu"
              className="flex flex-col items-center justify-center py-2 px-4 text-gray-700 hover:text-amber-600 transition"
            >
              <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
                <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z" />
              </svg>
              <span className="text-xs mt-1">Menu</span>
            </Link>
            <Link
              href="/orders"
              className="flex flex-col items-center justify-center py-2 px-4 text-gray-700 hover:text-amber-600 transition"
            >
              <IconContext.Provider value={{ size: "1.5em" }}>
                <FaReceipt />
              </IconContext.Provider>
              <span className="text-xs mt-1">Orders</span>
            </Link>
            <Link
              href="/cart"
              className="flex flex-col items-center justify-center py-2 px-4 text-gray-700 hover:text-amber-600 transition"
            >
              <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
                <path d="M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-0.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49A1.003 1.003 0 0 0 20 4H5.21l-.94-2H1zm16 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-0.9-2-2-2z" />
              </svg>
              <span className="text-xs mt-1">Cart</span>
            </Link>
            <button
              onClick={() => signOut()}
              className="flex flex-col items-center justify-center py-2 px-4 text-gray-700 hover:text-red-600 transition"
            >
              <IconContext.Provider value={{ size: "1.5em" }}>
                <IoLogOut />
              </IconContext.Provider>
              <span className="text-xs mt-1">Logout</span>
            </button>
          </div>
        </nav>
      )}
    </>
  );
}
