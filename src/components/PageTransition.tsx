// Global styles for smooth transitions
import type { ReactNode } from "react";

export const pageTransitionStyles = `
  @keyframes fadeInUp {
    from {
      opacity: 0;
      transform: translateY(20px);
    }
    to {
      opacity: 1;
      transform: translateY(0);
    }
  }

  @keyframes fadeIn {
    from {
      opacity: 0;
    }
    to {
      opacity: 1;
    }
  }

  @keyframes slideInRight {
    from {
      opacity: 0;
      transform: translateX(20px);
    }
    to {
      opacity: 1;
      transform: translateX(0);
    }
  }

  .page-transition {
    animation: fadeInUp 0.4s ease-out;
  }

  .fade-in {
    animation: fadeIn 0.3s ease-out;
  }

  .slide-in-right {
    animation: slideInRight 0.3s ease-out;
  }

  /* Smooth loading pulse */
  @keyframes shimmer {
    0% {
      background-position: -1000px 0;
    }
    100% {
      background-position: 1000px 0;
    }
  }

  .shimmer {
    animation: shimmer 2s infinite;
    background: linear-gradient(
      to right,
      #f3f4f6 0%,
      #e5e7eb 50%,
      #f3f4f6 100%
    );
    background-size: 1000px 100%;
  }
`;

interface PageTransitionProps {
  children: ReactNode;
  delay?: number;
}

export function PageTransition({ children, delay = 0 }: PageTransitionProps) {
  return (
    <div
      style={{
        animation: `fadeInUp 0.4s ease-out`,
        animationDelay: `${delay}ms`,
        animationFillMode: "both",
      }}
    >
      {children}
    </div>
  );
}
