"use client";

import { NotificationContext } from "#components/context/Notification";
import { useContext } from "react";

export function useNotifications() {
  const context = useContext(NotificationContext);
  if (!context) {
    throw new Error(
      "useNotifications must be used within NotificationProvider"
    );
  }
  return context;
}
