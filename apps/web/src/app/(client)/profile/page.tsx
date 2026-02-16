"use client";

import dynamic from "next/dynamic";
import { Suspense } from "react";

const ProfilePageContent = dynamic(
  () =>
    import("@/components/ProfilePageContent").then((mod) => ({
      default: mod.ProfilePageContent,
    })),
  { ssr: false }
);

export default function ProfilePage() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center min-h-screen bg-gray-50">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-amber-600"></div>
        </div>
      }
    >
      <ProfilePageContent />
    </Suspense>
  );
}
