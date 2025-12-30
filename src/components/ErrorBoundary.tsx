"use client";

import { errorLogger } from "@/lib/logger";
import React, { ReactNode } from "react";

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: React.ErrorInfo | null;
}

export class ErrorBoundary extends React.Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null, errorInfo: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error, errorInfo: null };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    // Log error to monitoring service
    errorLogger.error(
      "React Error Boundary caught an error",
      {
        componentStack: errorInfo.componentStack,
        errorMessage: error.toString(),
      },
      error
    );

    this.setState({ errorInfo });
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null, errorInfo: null });
  };

  render() {
    if (this.state.hasError) {
      return (
        this.props.fallback || (
          <div className="min-h-screen bg-gray-50 flex items-center justify-center px-4">
            <div className="max-w-md w-full bg-white rounded-lg shadow-lg p-6">
              <div className="text-center">
                <div className="text-5xl mb-4">⚠️</div>
                <h1 className="text-2xl font-bold text-gray-900 mb-2">
                  Oops! Something went wrong
                </h1>
                <p className="text-gray-600 mb-6">
                  {this.state.error?.message ||
                    "An unexpected error occurred. Please try refreshing the page."}
                </p>

                {process.env.NODE_ENV === "development" && (
                  <details className="mt-4 p-3 bg-red-50 rounded-lg text-left overflow-auto max-h-48 mb-6">
                    <summary className="cursor-pointer font-semibold text-red-700 mb-2">
                      Error Details (Dev Only)
                    </summary>
                    <pre className="text-xs text-red-600 whitespace-pre-wrap break-words">
                      {this.state.error?.stack}
                      {"\n\n"}
                      {this.state.errorInfo?.componentStack}
                    </pre>
                  </details>
                )}

                <div className="flex gap-3">
                  <button
                    onClick={this.handleReset}
                    className="flex-1 px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 transition font-semibold"
                  >
                    Try Again
                  </button>
                  <button
                    onClick={() => (window.location.href = "/")}
                    className="flex-1 px-4 py-2 bg-gray-300 text-gray-700 rounded-lg hover:bg-gray-400 transition font-semibold"
                  >
                    Go Home
                  </button>
                </div>
              </div>
            </div>
          </div>
        )
      );
    }

    return this.props.children;
  }
}

/**
 * Higher-order component to wrap components with error boundary
 */
export function withErrorBoundary<P extends object>(
  Component: React.ComponentType<P>,
  fallback?: ReactNode
) {
  return function WithErrorBoundary(props: P) {
    return (
      <ErrorBoundary fallback={fallback}>
        <Component {...props} />
      </ErrorBoundary>
    );
  };
}
