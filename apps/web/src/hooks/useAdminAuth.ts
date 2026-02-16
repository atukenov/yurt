/**
 * Admin Auth Protection Hook
 * Reusable hook for protecting admin routes
 */

import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

type AuthStatus =
  | "loading"
  | "authenticated"
  | "unauthenticated"
  | "unauthorized";

interface useAdminAuthReturn {
  status: AuthStatus;
  isAdmin: boolean;
  isLoading: boolean;
}

/**
 * Hook to protect admin routes
 * Automatically redirects to login if unauthenticated
 * Automatically redirects to home if not admin
 */
export function useAdminAuth(): useAdminAuthReturn {
  const router = useRouter();
  const { data: session, status } = useSession();

  useEffect(() => {
    if (status === "loading") {
      return;
    }

    if (status === "unauthenticated") {
      router.push("/login");
      return;
    }

    if (session?.user?.role !== "admin") {
      router.push("/");
      return;
    }
  }, [status, session, router]);

  return {
    status: status as AuthStatus,
    isAdmin: session?.user?.role === "admin",
    isLoading: status === "loading",
  };
}
