"use client";

import { useEffect } from "react";

import { clearCustomerStorage } from "#utils/helper/customerStorage";
import { signOut, useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { Spinner } from "xtreme-ui";

export default function Logout() {
  const router = useRouter();
  const session = useSession();

  useEffect(() => {
    if (session?.status === "authenticated") {
      const logoutData: any = {
        role: session?.data?.role,
      };

      if (session?.data?.restaurant?.username) {
        logoutData.restaurant = session.data.restaurant.username;
      }

      // Get table from URL or referrer
      if (typeof window !== "undefined") {
        const urlParams = new URLSearchParams(window.location.search);
        const tableFromUrl = urlParams.get("table");

        // Try to get table from document.referrer if not in URL
        if (!tableFromUrl && document.referrer) {
          try {
            const referrerUrl = new URL(document.referrer);
            const tableFromReferrer = referrerUrl.searchParams.get("table");
            if (tableFromReferrer) logoutData.table = tableFromReferrer;
          } catch (e) {
            // Ignore invalid referrer URLs
          }
        } else if (tableFromUrl) {
          logoutData.table = tableFromUrl;
        }
      }

      localStorage.setItem("logoutData", JSON.stringify(logoutData));

      // Clear customer storage to prevent auto-login
      clearCustomerStorage();

      // Sign out without automatic redirect
      signOut({ redirect: false }).then(() => {
        // Force router to handle the redirect after sign out is complete
        const { role, restaurant, table } = logoutData;

        if (role === "admin" || role === "kitchen") {
          router.replace("/");
        } else if (role === "customer" && restaurant) {
          router.replace(`/${restaurant}`);
        } else {
          router.replace("/");
        }
      });
    } else if (session?.status === "unauthenticated") {
      // If already unauthenticated, check if we have stored logout data
      try {
        const logoutDataStr = localStorage.getItem("logoutData");

        if (logoutDataStr) {
          const { role, restaurant, table } = JSON.parse(logoutDataStr);
          localStorage.removeItem("logoutData");

          if (role === "admin" || role === "kitchen") {
            router.replace("/");
          } else if (role === "customer" && restaurant && table) {
            router.replace(`/${restaurant}?table=${table}`);
          } else if (role === "customer" && restaurant) {
            router.replace(`/${restaurant}`);
          } else {
            router.replace("/");
          }
        } else {
          router.replace("/");
        }
      } catch (err) {
        console.log(err);
        localStorage.removeItem("logoutData");
        router.replace("/");
      }
    }
  }, [router, session]);

  return (
    <>
      <Spinner fullpage label="Signing out..." />
    </>
  );
}
