"use client";

import { useEffect } from "react";

/**
 * Registers the service worker for PWA support.
 * Renders nothing — drop this anywhere in the component tree.
 */
export function ServiceWorkerRegistrar() {
  useEffect(() => {
    if (
      typeof window !== "undefined" &&
      "serviceWorker" in navigator &&
      process.env.NODE_ENV === "production"
    ) {
      navigator.serviceWorker
        .register("/sw.js")
        .then((reg) => {
          console.log("SW registered, scope:", reg.scope);
        })
        .catch((err) => {
          console.warn("SW registration failed:", err);
        });
    }
  }, []);

  return null;
}
