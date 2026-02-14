"use client";

import Image from "next/image";
import { useEffect, useState } from "react";

export function SplashScreen() {
  const [isVisible, setIsVisible] = useState(true);

  useEffect(() => {
    // Hide splash screen after 2 seconds minimum
    // In production, this would wait for actual data to load
    const timer = setTimeout(() => {
      setIsVisible(false);
    }, 2000);

    return () => clearTimeout(timer);
  }, []);

  if (!isVisible) {
    return null;
  }

  return (
    <div className="fixed inset-0 bg-white flex items-center justify-center z-50">
      {/* Logo Container */}
      <div className="flex flex-col items-center gap-6 animate-fade-in">
        {/* Coffee Cup Logo */}
        <div className="relative w-48 sm:w-64 md:w-80">
          <Image
            src="/images/splash-logo.png"
            width={500}
            height={50}
            alt="logo"
            priority
          />
        </div>

        {/* Loading Indicator */}
        <div className="mt-8 flex gap-2">
          <div
            className="w-2 h-2 bg-[#fed118] rounded-full animate-bounce"
            style={{ animationDelay: "0ms" }}
          ></div>
          <div
            className="w-2 h-2 bg-[#fed118] rounded-full animate-bounce"
            style={{ animationDelay: "150ms" }}
          ></div>
          <div
            className="w-2 h-2 bg-[#fed118] rounded-full animate-bounce"
            style={{ animationDelay: "300ms" }}
          ></div>
        </div>
      </div>

      {/* Decorative Elements */}
      <div className="absolute top-10 left-10 w-20 h-20 bg-white opacity-5 rounded-full blur-2xl"></div>
      <div className="absolute bottom-10 right-10 w-32 h-32 bg-white opacity-5 rounded-full blur-3xl"></div>
    </div>
  );
}
