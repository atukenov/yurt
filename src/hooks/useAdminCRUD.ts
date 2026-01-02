/**
 * Reusable Admin CRUD Hook
 * Eliminates boilerplate code across admin pages (Menu, Toppings, Locations)
 */

import { useToastNotification } from "@/components/ToastProvider";
import { useSession } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

interface AdminCRUDHookOptions<T> {
  entityName: string;
  apiEndpoint: string;
  requiredRole?: "admin" | "customer";
  onFetchSuccess?: (data: T[]) => void;
  onFetchError?: (error: Error) => void;
}

interface AdminCRUDHookReturn<T> {
  items: T[];
  loading: boolean;
  editingId: string | null;
  showForm: boolean;
  message: { type: "success" | "error"; text: string } | null;
  setShowForm: (show: boolean) => void;
  setEditingId: (id: string | null) => void;
  setMessage: (msg: { type: "success" | "error"; text: string } | null) => void;
  setItems: (items: T[]) => void;
  fetchItems: () => Promise<void>;
  handleCreate: (data: unknown) => Promise<void>;
  handleUpdate: (id: string, data: unknown) => Promise<void>;
  handleDelete: (id: string) => Promise<void>;
  showMessage: (text: string, type: "success" | "error") => void;
}

/**
 * Reusable hook for admin CRUD operations
 * Handles auth, data fetching, and common CRUD patterns
 */
export function useAdminCRUD<T extends { _id?: string; id?: string }>(
  options: AdminCRUDHookOptions<T>
): AdminCRUDHookReturn<T> {
  const router = useRouter();
  const { data: session, status } = useSession();
  const { showToast } = useToastNotification();

  const [items, setItems] = useState<T[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [message, setMessage] = useState<{
    type: "success" | "error";
    text: string;
  } | null>(null);

  const showMessage = useCallback(
    (text: string, type: "success" | "error") => {
      setMessage({ type, text });
      showToast(text, type);
      setTimeout(() => setMessage(null), 4000);
    },
    [showToast]
  );

  const fetchItems = useCallback(async () => {
    try {
      setLoading(true);
      const res = await fetch(options.apiEndpoint);
      if (res.ok) {
        const data = await res.json();
        // Handle different API response structures
        const itemsData = data.items || data.toppings || data.locations || [];
        setItems(itemsData);
        options.onFetchSuccess?.(itemsData);
      }
    } catch (error) {
      const err = error instanceof Error ? error : new Error(String(error));
      showMessage(`Failed to load ${options.entityName}`, "error");
      options.onFetchError?.(err);
    } finally {
      setLoading(false);
    }
  }, [options, showMessage]);

  // Auth check on mount
  useEffect(() => {
    if (status === "loading") return;

    if (status === "unauthenticated") {
      void router.push("/login");
      return;
    }

    if (session?.user?.role !== (options.requiredRole || "admin")) {
      void router.push("/");
      return;
    }

    void fetchItems();
  }, [status, session, router, options.requiredRole, fetchItems]);

  const handleCreate = useCallback(
    async (data: unknown) => {
      try {
        const res = await fetch(options.apiEndpoint, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(data),
        });

        if (res.ok) {
          showMessage(`${options.entityName} created successfully`, "success");
          await fetchItems();
          setShowForm(false);
        } else {
          const errorData = await res.json();
          showMessage(
            errorData.error || `Failed to create ${options.entityName}`,
            "error"
          );
        }
      } catch {
        showMessage(`Error creating ${options.entityName}`, "error");
      }
    },
    [options.apiEndpoint, options.entityName, fetchItems, showMessage]
  );

  const handleUpdate = useCallback(
    async (id: string, data: unknown) => {
      try {
        const res = await fetch(`${options.apiEndpoint}/${id}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(data),
        });

        if (res.ok) {
          showMessage(`${options.entityName} updated successfully`, "success");
          await fetchItems();
          setEditingId(null);
          setShowForm(false);
        } else {
          const errorData = await res.json();
          showMessage(
            errorData.error || `Failed to update ${options.entityName}`,
            "error"
          );
        }
      } catch {
        showMessage(`Error updating ${options.entityName}`, "error");
      }
    },
    [options.apiEndpoint, options.entityName, fetchItems, showMessage]
  );

  const handleDelete = useCallback(
    async (id: string) => {
      if (
        !confirm(`Are you sure you want to delete this ${options.entityName}?`)
      ) {
        return;
      }

      try {
        const res = await fetch(`${options.apiEndpoint}/${id}`, {
          method: "DELETE",
        });

        if (res.ok) {
          showMessage(`${options.entityName} deleted successfully`, "success");
          await fetchItems();
        } else {
          showMessage(`Failed to delete ${options.entityName}`, "error");
        }
      } catch {
        showMessage(`Error deleting ${options.entityName}`, "error");
      }
    },
    [options.apiEndpoint, options.entityName, fetchItems, showMessage]
  );

  return {
    items,
    loading,
    editingId,
    showForm,
    message,
    setShowForm,
    setEditingId,
    setMessage,
    setItems,
    fetchItems,
    handleCreate,
    handleUpdate,
    handleDelete,
    showMessage,
  };
}
