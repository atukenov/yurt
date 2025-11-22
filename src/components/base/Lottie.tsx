"use client";

import { useEffect, useRef } from "react";

import { DotLottie } from "@lottiefiles/dotlottie-web";

interface LottieProps {
  src: string;
  className?: string;
  speed?: number;
  loop?: boolean;
  autoplay?: boolean;
}

/**
 * Lottie animation player using @lottiefiles/dotlottie-web.
 * Natively supports .lottie files and renders SVG animations.
 */
export default function Lottie({
  src,
  className = "",
  speed = 1,
  loop = true,
  autoplay = true,
}: LottieProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const playerRef = useRef<DotLottie | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const initLottie = async () => {
      try {
        // Destroy previous player if exists
        if (playerRef.current) {
          playerRef.current.destroy();
          playerRef.current = null;
        }

        // Create DotLottie player instance
        const dotLottie = new DotLottie({
          autoplay,
          loop,
          speed,
          canvas,
        });

        // Load animation from .lottie file
        await dotLottie.load({ src });

        playerRef.current = dotLottie;
      } catch (error) {
        console.error("Failed to initialize Lottie:", error);
      }
    };

    initLottie();

    return () => {
      if (playerRef.current) {
        playerRef.current.destroy();
        playerRef.current = null;
      }
    };
  }, [src, autoplay, loop, speed]);

  return (
    <canvas
      ref={canvasRef}
      className={`lottie-container ${className}`}
      style={{ width: "100%", height: "100%", display: "block" }}
    />
  );
}
