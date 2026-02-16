"use client";

import { TOAST_CONFIG, ToastType } from "@/lib/constants";
import { useState } from "react";

export interface Toast {
  id: string;
  message: string;
  type: ToastType;
  duration?: number;
}

/**
 * Legacy toast hook - use ToastProvider + useToastNotification for new code
 * Kept for backward compatibility with existing components
 */
export const useToast = () => {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const showToast = (
    message: string,
    type: ToastType = "info",
    duration = TOAST_CONFIG.DEFAULT_DURATION
  ) => {
    const id = Date.now().toString();
    const newToast: Toast = { id, message, type, duration };

    setToasts((prev) => [...prev, newToast]);

    if (duration > 0) {
      setTimeout(() => {
        setToasts((prev) => prev.filter((t) => t.id !== id));
      }, duration);
    }

    return id;
  };

  const removeToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  return { toasts, showToast, removeToast };
};

export function ToastContainer() {
  const { toasts, removeToast } = useToast();

  return (
    <div className="fixed top-4 right-4 z-9999 space-y-2 max-w-md">
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

function ToastItem({ toast, onClose }: { toast: Toast; onClose: () => void }) {
  const bgColor = TOAST_CONFIG.COLORS[toast.type];
  const icon = TOAST_CONFIG.ICONS[toast.type];

  return (
    <div
      className={`${bgColor} text-white px-4 py-3 rounded-lg shadow-lg flex items-start justify-between animate-in slide-in-from-top-2 fade-in duration-300`}
    >
      <div className="flex items-start gap-3">
        <span className="font-bold text-lg">{icon}</span>
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
