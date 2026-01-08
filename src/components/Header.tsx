"use client";

import { useLanguage } from "@/context/LanguageContext";
import { translations } from "@/lib/translations";
import { signOut, useSession } from "next-auth/react";
import Image from "next/image";
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
  const pathname = usePathname() || "";
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [showSearch, setShowSearch] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [managementOpen, setManagementOpen] = useState(false);
  const searchInputRef = useRef<HTMLInputElement | null>(null);

  // Safely access language context with fallback
  let language: "en" | "ru" | "ar" = "en";
  let t = translations.en.common;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.common || translations.en.common;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.common;
  }

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
                    placeholder={t.searchMenu}
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
            <Image
              src="/images/logo.png"
              alt="altyncup logo"
              width={150}
              height={100}
            />

            {/* Desktop Navigation */}
            <nav className="hidden md:flex items-center gap-6">
              {session?.user ? (
                <>
                  {session.user.role === "admin" ? (
                    <>
                      <Link
                        href="/admin"
                        className={`transition font-semibold ${
                          pathname === "/admin" ||
                          (pathname.startsWith("/admin") &&
                            !pathname.includes("/analytics") &&
                            !pathname.includes("/menu") &&
                            !pathname.includes("/toppings") &&
                            !pathname.includes("/locations"))
                            ? "text-amber-600 border-b-2 border-amber-600"
                            : "text-gray-700 hover:text-amber-600"
                        }`}
                      >
                        {t.dashboard}
                      </Link>
                      <Link
                        href="/admin/analytics"
                        className={`transition ${
                          pathname === "/admin/analytics"
                            ? "text-amber-600 border-b-2 border-amber-600"
                            : "text-gray-700 hover:text-amber-600"
                        }`}
                      >
                        {t.analytics}
                      </Link>
                      {/* Management Dropdown */}
                      <div className="relative">
                        <button
                          onClick={() => setManagementOpen(!managementOpen)}
                          className={`transition flex items-center gap-1 ${
                            pathname.includes("/admin/menu") ||
                            pathname.includes("/admin/toppings") ||
                            pathname.includes("/admin/locations")
                              ? "text-amber-600 border-b-2 border-amber-600"
                              : "text-gray-700 hover:text-amber-600"
                          }`}
                        >
                          {t.management}
                          <MdExpandMore size={18} />
                        </button>
                        {managementOpen && (
                          <div className="absolute left-0 mt-2 w-48 bg-white rounded-lg shadow-lg z-50 border border-gray-200">
                            <Link
                              href="/admin/menu"
                              className={`block px-4 py-2 rounded-t-lg transition ${
                                pathname === "/admin/menu"
                                  ? "bg-amber-100 text-amber-700 font-semibold"
                                  : "text-gray-700 hover:bg-amber-50"
                              }`}
                              onClick={() => setManagementOpen(false)}
                            >
                              {t.menu}
                            </Link>
                            <Link
                              href="/admin/toppings"
                              className={`block px-4 py-2 transition ${
                                pathname === "/admin/toppings"
                                  ? "bg-amber-100 text-amber-700 font-semibold"
                                  : "text-gray-700 hover:bg-amber-50"
                              }`}
                              onClick={() => setManagementOpen(false)}
                            >
                              {t.toppings}
                            </Link>
                            <Link
                              href="/admin/locations"
                              className={`block px-4 py-2 rounded-b-lg transition ${
                                pathname === "/admin/locations"
                                  ? "bg-amber-100 text-amber-700 font-semibold"
                                  : "text-gray-700 hover:bg-amber-50"
                              }`}
                              onClick={() => setManagementOpen(false)}
                            >
                              {t.locations}
                            </Link>
                          </div>
                        )}
                      </div>
                    </>
                  ) : (
                    <>
                      <Link
                        href="/menu"
                        className={`transition ${
                          pathname === "/menu"
                            ? "text-amber-600 border-b-2 border-amber-600"
                            : "text-gray-700 hover:text-amber-600"
                        }`}
                      >
                        {t.menu}
                      </Link>
                      <Link
                        href="/orders"
                        className={`transition ${
                          pathname === "/orders"
                            ? "text-amber-600 border-b-2 border-amber-600"
                            : "text-gray-700 hover:text-amber-600"
                        }`}
                      >
                        {t.myOrders}
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
                    {t.login}
                  </Link>
                  <Link
                    href="/register"
                    className="px-4 py-2 bg-[#ffd119] text-black rounded-lg hover:bg-amber-700 transition"
                  >
                    {t.register}
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
                        {t.dashboard}
                      </Link>
                      <Link
                        href="/admin/analytics"
                        className="block py-2 text-gray-700 hover:text-amber-600"
                      >
                        {t.analytics}
                      </Link>
                      <button
                        onClick={() => setManagementOpen(!managementOpen)}
                        className="w-full text-left py-2 text-gray-700 hover:text-amber-600 flex items-center justify-between"
                      >
                        {t.management}
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
                            {t.menu}
                          </Link>
                          <Link
                            href="/admin/toppings"
                            onClick={() => setManagementOpen(false)}
                            className="block py-2 text-gray-700 hover:text-amber-600 text-sm"
                          >
                            {t.toppings}
                          </Link>
                          <Link
                            href="/admin/locations"
                            onClick={() => setManagementOpen(false)}
                            className="block py-2 text-gray-700 hover:text-amber-600 text-sm"
                          >
                            {t.locations}
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
                        {t.menu}
                      </Link>
                      <Link
                        href="/orders"
                        className="block py-2 text-gray-700 hover:text-amber-600"
                      >
                        {t.myOrders}
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
                    {t.login}
                  </Link>
                  <Link
                    href="/register"
                    className="block py-2 text-gray-700 hover:text-amber-600"
                  >
                    {t.register}
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
