"use client";

import { Suspense } from "react";
import MenuContent from "./MenuContent";

function MenuLoading() {
  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="text-center py-12">
        <p>Loading menu...</p>
      </div>
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
