"use client";

import { signOut, useSession } from "next-auth/react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { IconContext } from "react-icons";
import { IoLogOut } from "react-icons/io5";
import { MdExpandMore } from "react-icons/md";
import { CartHeaderIcons } from "./CartHeaderIcons";
import { LocationSelector } from "./LocationSelector";
import { MobileBottomNav } from "./MobileBottomNav";

export function Header() {
  const { data: session } = useSession();
  const router = useRouter();
  const pathname = usePathname();
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [showSearch, setShowSearch] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [managementOpen, setManagementOpen] = useState(false);
  const searchInputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (showSearch && searchInputRef.current) {
      searchInputRef.current.focus();
    }
  }, [showSearch]);

  const handleSearchChange = (query: string) => {
    setSearchQuery(query);
    if (pathname === "/menu") {
      const url = query ? `/menu?search=${encodeURIComponent(query)}` : "/menu";
      router.push(url);
    }
  };

  return (
    <>
      {/* Customer Header - Top Navigation */}
      {session?.user?.role === "customer" && (
        <header className="sticky top-0 z-40 bg-white border-b border-gray-200 shadow-sm">
          <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex justify-between items-center h-16 gap-4">
              {/* Left: Location Selector or Search Input */}
              <div className={showSearch ? "flex-1 min-w-0" : "flex-shrink-0"}>
                {showSearch ? (
                  <input
                    ref={searchInputRef}
                    type="text"
                    placeholder="Search menu..."
                    value={searchQuery}
                    onChange={(e) => handleSearchChange(e.target.value)}
                    className="w-full px-4 py-2 border border-gray-300 rounded-lg text-gray-700 focus:outline-none focus:border-amber-600 focus:ring-1 focus:ring-amber-600"
                    onKeyDown={(e) => {
                      if (e.key === "Escape") {
                        setShowSearch(false);
                        setSearchQuery("");
                      }
                    }}
                    autoFocus
                  />
                ) : (
                  <LocationSelector />
                )}
              </div>

              {/* Right: Search, Favorites, Cart */}
              <CartHeaderIcons
                showSearch={showSearch}
                setShowSearch={setShowSearch}
                setSearchQuery={setSearchQuery}
                searchInputRef={searchInputRef}
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
                        href="/admin/analytics"
                        className="text-gray-700 hover:text-amber-600 transition"
                      >
                        Analytics
                      </Link>
                      {/* Management Dropdown */}
                      <div className="relative">
                        <button
                          onClick={() => setManagementOpen(!managementOpen)}
                          className="text-gray-700 hover:text-amber-600 transition flex items-center gap-1"
                        >
                          Management
                          <MdExpandMore size={18} />
                        </button>
                        {managementOpen && (
                          <div className="absolute left-0 mt-2 w-48 bg-white rounded-lg shadow-lg z-50 border border-gray-200">
                            <Link
                              href="/admin/menu"
                              className="block px-4 py-2 text-gray-700 hover:bg-amber-50 rounded-t-lg"
                              onClick={() => setManagementOpen(false)}
                            >
                              Menu
                            </Link>
                            <Link
                              href="/admin/toppings"
                              className="block px-4 py-2 text-gray-700 hover:bg-amber-50"
                              onClick={() => setManagementOpen(false)}
                            >
                              Toppings
                            </Link>
                            <Link
                              href="/admin/locations"
                              className="block px-4 py-2 text-gray-700 hover:bg-amber-50 rounded-b-lg"
                              onClick={() => setManagementOpen(false)}
                            >
                              Locations
                            </Link>
                          </div>
                        )}
                      </div>
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
                        href="/admin/analytics"
                        className="block py-2 text-gray-700 hover:text-amber-600"
                      >
                        Analytics
                      </Link>
                      <button
                        onClick={() => setManagementOpen(!managementOpen)}
                        className="w-full text-left py-2 text-gray-700 hover:text-amber-600 flex items-center justify-between"
                      >
                        Management
                        <MdExpandMore
                          size={18}
                          className={`transition-transform ${
                            managementOpen ? "rotate-180" : ""
                          }`}
                        />
                      </button>
                      {managementOpen && (
                        <div className="ml-4 border-l border-gray-300 pl-4">
                          <Link
                            href="/admin/menu"
                            onClick={() => setManagementOpen(false)}
                            className="block py-2 text-gray-700 hover:text-amber-600 text-sm"
                          >
                            Menu
                          </Link>
                          <Link
                            href="/admin/toppings"
                            onClick={() => setManagementOpen(false)}
                            className="block py-2 text-gray-700 hover:text-amber-600 text-sm"
                          >
                            Toppings
                          </Link>
                          <Link
                            href="/admin/locations"
                            onClick={() => setManagementOpen(false)}
                            className="block py-2 text-gray-700 hover:text-amber-600 text-sm"
                          >
                            Locations
                          </Link>
                        </div>
                      )}
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
      <MobileBottomNav />
    </>
  );
}
