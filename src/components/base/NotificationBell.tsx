"use client";

import { useNotifications } from "#utils/hooks/useNotifications";
import "./notificationBell.scss";

export function NotificationBell() {
  const { notifications, isConnected } = useNotifications();

  return (
    <div className="notificationBell">
      <div className={`bell ${isConnected ? "connected" : "disconnected"}`}>
        🔔
        {notifications.length > 0 && (
          <span className="badge">{notifications.length}</span>
        )}
      </div>
      <div className="status">
        {isConnected ? (
          <span className="connected-dot" title="Connected" />
        ) : (
          <span className="disconnected-dot" title="Reconnecting..." />
        )}
      </div>
    </div>
  );
}
