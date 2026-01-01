"use client";

import { signOut, useSession } from "next-auth/react";
import Link from "next/link";
import { IconContext } from "react-icons";
import { FaReceipt } from "react-icons/fa6";
import { IoLogOut } from "react-icons/io5";

export function MobileBottomNav() {
  const { data: session } = useSession();

  if (!session?.user || session.user.role !== "customer") {
    return null;
  }

  return (
    <nav className="md:hidden fixed bottom-0 left-0 right-0 z-40 bg-white border-t border-gray-200 shadow-lg">
      <div className="flex justify-around items-center h-16 pb-0">
        <Link
          href="/menu"
          className="flex flex-col items-center justify-center py-2 px-4 text-gray-700 hover:text-amber-600 transition flex-1"
          title="Browse menu"
        >
          <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
            <path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm3.5-9c.83 0 1.5-.67 1.5-1.5S16.33 8 15.5 8 14 8.67 14 9.5s.67 1.5 1.5 1.5zm-7 0c.83 0 1.5-.67 1.5-1.5S9.33 8 8.5 8 7 8.67 7 9.5 7.67 11 8.5 11zm3.5 6.5c2.33 0 4.31-1.46 5.11-3.5H6.89c.8 2.04 2.78 3.5 5.11 3.5z" />
          </svg>
          <span className="text-xs mt-1">Menu</span>
        </Link>

        <Link
          href="/orders"
          className="flex flex-col items-center justify-center py-2 px-4 text-gray-700 hover:text-amber-600 transition flex-1"
          title="Your orders"
        >
          <IconContext.Provider value={{ size: "1.5em" }}>
            <FaReceipt />
          </IconContext.Provider>
          <span className="text-xs mt-1">Orders</span>
        </Link>

        <Link
          href="/profile"
          className="flex flex-col items-center justify-center py-2 px-4 text-gray-700 hover:text-amber-600 transition flex-1"
          title="Profile settings"
        >
          <svg className="w-6 h-6" fill="currentColor" viewBox="0 0 24 24">
            <path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z" />
          </svg>
          <span className="text-xs mt-1">Profile</span>
        </Link>

        <button
          onClick={() => signOut()}
          className="flex flex-col items-center justify-center py-2 px-4 text-gray-700 hover:text-red-600 transition flex-1"
          title="Logout"
        >
          <IconContext.Provider value={{ size: "1.5em" }}>
            <IoLogOut />
          </IconContext.Provider>
          <span className="text-xs mt-1">Logout</span>
        </button>
      </div>
    </nav>
  );
}
