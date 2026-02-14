"use client";

import { TOAST_CONFIG, ToastType } from "@/lib/constants";
import React, { createContext, useCallback, useContext } from "react";

export interface NotificationToast {
  id: string;
  message: string;
  type: ToastType;
  duration?: number;
}

export interface ToastContextType {
  showToast: (message: string, type?: ToastType, duration?: number) => void;
  toasts: NotificationToast[];
  removeToast: (id: string) => void;
}

export const ToastContext = createContext<ToastContextType | undefined>(
  undefined
);

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = React.useState<NotificationToast[]>([]);

  const showToast = useCallback(
    (message: string, type: ToastType = "info", duration?: number) => {
      const id = Date.now().toString();
      const finalDuration = duration ?? TOAST_CONFIG.DEFAULT_DURATION;

      setToasts((prev) => [
        ...prev,
        { id, message, type, duration: finalDuration },
      ]);

      if (finalDuration > 0) {
        setTimeout(() => {
          setToasts((prev) => prev.filter((t) => t.id !== id));
        }, finalDuration);
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
  const bgColor = TOAST_CONFIG.COLORS[toast.type];
  const icon = TOAST_CONFIG.ICONS[toast.type];

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
