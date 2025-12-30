"use client";

import { MenuGridSkeleton } from "@/components/SkeletonLoaders";
import { Suspense } from "react";
import MenuContent from "./MenuContent";

function MenuLoading() {
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <MenuGridSkeleton count={9} />
    </div>
  );
}

export default function MenuPage() {
  return (
    <Suspense fallback={<MenuLoading />}>
      <MenuContent />
    </Suspense>
  );
}
