import type { NextApiRequest, NextApiResponse } from "next";

/**
 * Socket.io Integration Note:
 *
 * Socket.io requires a raw HTTP server instance, which Next.js doesn't expose by default.
 * The real-time notification system is designed with:
 *
 * 1. **Polling Fallback** (Default in development):
 *    - Admin dashboard polls every 10 seconds
 *    - Customer orders page polls every 10 seconds
 *    - Works reliably without additional server setup
 *
 * 2. **WebSocket Support** (For production):
 *    - Option A: Deploy with custom Node.js server using socket.io
 *    - Option B: Use Vercel/Edge Functions with Socket.io adapters
 *    - Option C: Use a separate Socket.io server on different port
 *
 * For development, the polling fallback is sufficient and automatically
 * used if Socket.io is not available.
 */

export default function handler(req: NextApiRequest, res: NextApiResponse) {
  res.status(200).json({
    status: "ok",
    message: "Socket.io polling fallback active",
  });
}
