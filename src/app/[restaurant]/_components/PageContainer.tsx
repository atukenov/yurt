"use client";

import { useSearchParams } from "next/navigation";

import UnderConstruction from "#components/layout/UnderConstruction";

import OrderPage from "./Menu/OrderPage";
import ReviewsPage from "./Menu/ReviewsPage";
import ContactPage from "./Menu/ContactPage";

export default function PageContainer() {
  const searchParams = useSearchParams();
  const tab = searchParams.get("tab");

  return (
    <div className="pageContainer">
      {tab === "explore" && <UnderConstruction />}
      {tab === "menu" && <OrderPage />}
      {tab === "reviews" && <ReviewsPage />}
      {tab === "contact" && <ContactPage />}
    </div>
  );
}
