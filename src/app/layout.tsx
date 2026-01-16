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
  title: "Altyncup",
  description: "",
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
              </SocketProvider>
            </AuthProvider>
          </ToastProvider>
        </LanguageProvider>
      </body>
    </html>
  );
}
