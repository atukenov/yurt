"use client";

import { useLanguage } from "@/context/LanguageContext";
import { translations } from "@/lib/translations";
import { signOut, useSession } from "next-auth/react";
import Link from "next/link";
import { IconContext } from "react-icons";
import { BiCoffeeTogo } from "react-icons/bi";
import { IoLogOut, IoPersonSharp, IoReceiptSharp } from "react-icons/io5";

export function MobileBottomNav() {
  const { data: session } = useSession();

  // Safely access language context with fallback
  let language: "en" | "ru" = "en";
  let t = translations.en.client;
  try {
    const langContext = useLanguage();
    language = langContext.language;
    t = translations[language]?.client || translations.en.client;
  } catch (e) {
    // If language context not available, use English as default
    t = translations.en.client;
  }

  if (!session?.user || session.user.role !== "customer") {
    return null;
  }

  return (
    <nav className="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-white border-t border-gray-200 shadow-lg">
      <div className="flex justify-around items-center h-16 w-full overflow-hidden">
        <Link
          href="/menu"
          className="flex flex-col items-center justify-center py-1 px-2 text-gray-700 hover:text-amber-600 transition flex-1 h-full"
          title={t.menu}
        >
          <IconContext.Provider value={{ size: "1.25em", color: "#ffd119" }}>
            <BiCoffeeTogo />
          </IconContext.Provider>
          <span className="text-xs mt-0.5">{t.menu}</span>
        </Link>

        <Link
          href="/orders"
          className="flex flex-col items-center justify-center py-1 px-2 text-gray-700 hover:text-amber-600 transition flex-1 h-full"
          title={t.myOrders}
        >
          <IconContext.Provider value={{ size: "1.25em", color: "#ffd119" }}>
            <IoReceiptSharp />
          </IconContext.Provider>
          <span className="text-xs mt-0.5">{t.myOrders}</span>
        </Link>

        <Link
          href="/profile"
          className="flex flex-col items-center justify-center py-1 px-2 text-gray-700 hover:text-amber-600 transition flex-1 h-full"
          title={t.profileSettings}
        >
          <IconContext.Provider value={{ size: "1.25em", color: "#ffd119" }}>
            <IoPersonSharp />
          </IconContext.Provider>
          <span className="text-xs mt-0.5">{t.profileSettings}</span>
        </Link>

        <button
          onClick={() => signOut()}
          className="flex flex-col items-center justify-center py-1 px-2 text-gray-700 hover:text-red-600 transition flex-1 h-full"
          title={t.logout}
        >
          <IconContext.Provider value={{ size: "1.25em", color: "#ffd119" }}>
            <IoLogOut />
          </IconContext.Provider>
          <span className="text-xs mt-0.5">{t.logout}</span>
        </button>
      </div>
    </nav>
  );
}
