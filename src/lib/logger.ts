// Error logging and monitoring utility

export type ErrorLevel = "debug" | "info" | "warn" | "error" | "fatal";

export interface ErrorLog {
  level: ErrorLevel;
  message: string;
  context?: Record<string, any>;
  timestamp: Date;
  stack?: string;
  userAgent?: string;
  url?: string;
}

class ErrorLogger {
  private logs: ErrorLog[] = [];
  private maxLogs = 100;
  private isDevelopment = process.env.NODE_ENV === "development";

  /**
   * Log an error with context
   */
  log(
    level: ErrorLevel,
    message: string,
    context?: Record<string, any>,
    error?: Error
  ) {
    const errorLog: ErrorLog = {
      level,
      message,
      context,
      timestamp: new Date(),
      stack: error?.stack,
      userAgent:
        typeof navigator !== "undefined" ? navigator.userAgent : undefined,
      url: typeof window !== "undefined" ? window.location.href : undefined,
    };

    this.logs.push(errorLog);

    // Keep only recent logs
    if (this.logs.length > this.maxLogs) {
      this.logs = this.logs.slice(-this.maxLogs);
    }

    // Log to console in development
    if (this.isDevelopment) {
      const style = this.getConsoleStyle(level);
      console.log(
        `%c[${level.toUpperCase()}] ${message}`,
        style,
        context || "",
        error || ""
      );
    }

    // Send to monitoring service in production
    if (!this.isDevelopment && level === "error") {
      this.sendToMonitoring(errorLog);
    }
  }

  /**
   * Log debug message
   */
  debug(message: string, context?: Record<string, any>) {
    this.log("debug", message, context);
  }

  /**
   * Log info message
   */
  info(message: string, context?: Record<string, any>) {
    this.log("info", message, context);
  }

  /**
   * Log warning
   */
  warn(message: string, context?: Record<string, any>, error?: Error) {
    this.log("warn", message, context, error);
  }

  /**
   * Log error
   */
  error(message: string, context?: Record<string, any>, error?: Error) {
    this.log("error", message, context, error);
  }

  /**
   * Log fatal error
   */
  fatal(message: string, context?: Record<string, any>, error?: Error) {
    this.log("fatal", message, context, error);
    // Could trigger alert, reload page, or other recovery
  }

  /**
   * Get console style for log level
   */
  private getConsoleStyle(level: ErrorLevel): string {
    const styles: Record<ErrorLevel, string> = {
      debug: "color: #888; font-weight: normal;",
      info: "color: #0066cc; font-weight: normal;",
      warn: "color: #ff9900; font-weight: bold;",
      error: "color: #cc0000; font-weight: bold;",
      fatal: "color: #cc0000; font-weight: bold; background: #ffcccc;",
    };
    return styles[level];
  }

  /**
   * Send error to monitoring service (Sentry, LogRocket, etc.)
   */
  private sendToMonitoring(errorLog: ErrorLog) {
    // TODO: Implement actual monitoring service integration
    // Example: Sentry.captureException(error)
    if (typeof window !== "undefined") {
      // Could send to an API endpoint
      // fetch('/api/logs', { method: 'POST', body: JSON.stringify(errorLog) })
    }
  }

  /**
   * Get all logged errors
   */
  getLogs(): ErrorLog[] {
    return [...this.logs];
  }

  /**
   * Clear all logs
   */
  clearLogs() {
    this.logs = [];
  }

  /**
   * Export logs for debugging
   */
  exportLogs(): string {
    return JSON.stringify(this.logs, null, 2);
  }
}

// Singleton instance
export const errorLogger = new ErrorLogger();

/**
 * Format error message for display to user
 */
export function formatErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }
  if (typeof error === "string") {
    return error;
  }
  return "An unexpected error occurred. Please try again.";
}

/**
 * Get user-friendly error message for common errors
 */
export function getUserFriendlyErrorMessage(message: string): string {
  const errorMap: Record<string, string> = {
    "Network request failed":
      "Unable to connect to server. Please check your internet connection.",
    "401": "You are not authenticated. Please log in.",
    "403": "You don't have permission to perform this action.",
    "404": "The requested resource was not found.",
    "500": "Server error. Please try again later.",
    timeout: "Request timed out. Please try again.",
    ECONNREFUSED: "Unable to connect to server. Please try again later.",
  };

  for (const [key, value] of Object.entries(errorMap)) {
    if (message.includes(key)) {
      return value;
    }
  }

  return "Something went wrong. Please try again.";
}

/**
 * Handle async function errors
 */
export async function handleAsyncError<T>(
  fn: () => Promise<T>,
  errorContext?: Record<string, any>
): Promise<{ success: boolean; data?: T; error?: string }> {
  try {
    const data = await fn();
    return { success: true, data };
  } catch (error) {
    const message = formatErrorMessage(error);
    errorLogger.error(
      message,
      errorContext,
      error instanceof Error ? error : undefined
    );
    return { success: false, error: getUserFriendlyErrorMessage(message) };
  }
}
