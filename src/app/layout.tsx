import { AuthProvider } from "@/components/AuthProvider";
import { Header } from "@/components/Header";
import { SocketProvider } from "@/components/SocketProvider";
import { SplashScreen } from "@/components/SplashScreen";
import { ToastProvider } from "@/components/ToastProvider";
import { LanguageProvider } from "@/context/LanguageContext";
import type { Metadata } from "next";
import { Geist, Geist_Mono } from "next/font/google";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Yurt Coffee - Order Your Perfect Coffee",
  description:
    "Modern coffee ordering system with real-time order tracking and admin dashboard",
  keywords: "coffee, ordering, cafe, mobile-first",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link
          rel="preconnect"
          href="https://fonts.gstatic.com"
          crossOrigin="anonymous"
        />
        <link
          href="https://fonts.googleapis.com/css2?family=Archivo+Black&family=Bitter:ital,wght@0,100..900;1,100..900&display=swap"
          rel="stylesheet"
        />
      </head>
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased bg-gray-50`}
      >
        <SplashScreen />
        <LanguageProvider>
          <ToastProvider>
            <AuthProvider>
              <SocketProvider>
                <Header />
                <main className="md:min-h-screen">{children}</main>
                <footer className="bg-gray-900 text-white py-4">
                  <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                    <p className="text-center text-gray-400">
                      © 2025 Yurt Coffee. All rights reserved.
                    </p>
                  </div>
                </footer>
              </SocketProvider>
            </AuthProvider>
          </ToastProvider>
        </LanguageProvider>
      </body>
    </html>
  );
}
