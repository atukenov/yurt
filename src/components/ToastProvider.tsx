"use client";

import React, { createContext, useCallback, useContext } from "react";

interface NotificationToast {
  id: string;
  message: string;
  type: "success" | "error" | "info" | "warning";
  duration?: number;
}

interface ToastContextType {
  showToast: (
    message: string,
    type?: "success" | "error" | "info" | "warning",
    duration?: number
  ) => void;
  toasts: NotificationToast[];
  removeToast: (id: string) => void;
}

const ToastContext = createContext<ToastContextType | undefined>(undefined);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = React.useState<NotificationToast[]>([]);

  const showToast = useCallback(
    (
      message: string,
      type: "success" | "error" | "info" | "warning" = "info",
      duration = 4000
    ) => {
      const id = Date.now().toString();

      setToasts((prev) => [...prev, { id, message, type, duration }]);

      if (duration > 0) {
        setTimeout(() => {
          setToasts((prev) => prev.filter((t) => t.id !== id));
        }, duration);
      }
    },
    []
  );

  const removeToast = useCallback((id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  }, []);

  return (
    <ToastContext.Provider value={{ showToast, toasts, removeToast }}>
      {children}
      <ToastContainer />
    </ToastContext.Provider>
  );
}

export function useToastNotification() {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error("useToastNotification must be used within ToastProvider");
  }
  return context;
}

function ToastContainer() {
  const { toasts, removeToast } = useContext(ToastContext)!;

  return (
    <div className="fixed top-4 right-4 z-9999 space-y-2 max-w-md pointer-events-none">
      {toasts.map((toast) => (
        <ToastItem
          key={toast.id}
          toast={toast}
          onClose={() => removeToast(toast.id)}
        />
      ))}
    </div>
  );
}

function ToastItem({
  toast,
  onClose,
}: {
  toast: NotificationToast;
  onClose: () => void;
}) {
  const bgColor = {
    success: "bg-green-500",
    error: "bg-red-500",
    warning: "bg-yellow-500",
    info: "bg-blue-500",
  }[toast.type];

  const icon = {
    success: "✓",
    error: "✕",
    warning: "⚠",
    info: "ℹ",
  }[toast.type];

  return (
    <div
      className={`${bgColor} text-white px-4 py-3 rounded-lg shadow-lg flex items-start justify-between animate-in slide-in-from-top-2 fade-in duration-300 pointer-events-auto`}
    >
      <div className="flex items-start gap-3 flex-1">
        <span className="font-bold text-lg shrink-0">{icon}</span>
        <p className="text-sm font-medium">{toast.message}</p>
      </div>
      <button
        onClick={onClose}
        className="text-white hover:opacity-75 ml-2 shrink-0"
      >
        ✕
      </button>
    </div>
  );
}
