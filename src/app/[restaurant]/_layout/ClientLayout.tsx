"use client";

import { NotificationToast } from "#components/base/NotificationToast";
import { NotificationProvider } from "#components/context/Notification";
import { useSession } from "next-auth/react";
import { ReactNode } from "react";

interface RestaurantClientLayoutProps {
  children: ReactNode;
}

export function RestaurantClientLayout({
  children,
}: RestaurantClientLayoutProps) {
  const { data: session } = useSession();

  // Use customer ID from session, fallback to temporary ID
  const customerId = session?.customer?._id || `temp-customer-${Date.now()}`;

  return (
    <NotificationProvider type="customer" target={customerId}>
      <NotificationToast />
      {children}
    </NotificationProvider>
  );
}
