const { createServer } = require("http");
const { parse } = require("url");
const next = require("next");
const { Server: SocketIOServer } = require("socket.io");

const dev = process.env.NODE_ENV !== "production";
const app = next({ dev });
const handle = app.getRequestHandler();
const PORT = parseInt(process.env.PORT || "3000", 10);

app.prepare().then(() => {
  const server = createServer((req, res) => {
    const parsedUrl = parse(req.url, true);
    handle(req, res, parsedUrl);
  });

  // Initialize Socket.IO
  const io = new SocketIOServer(server, {
    cors: {
      origin: process.env.NEXTAUTH_URL || "http://localhost:3000",
      methods: ["GET", "POST"],
      credentials: true,
    },
    transports: ["websocket", "polling"],
  });

  // Setup namespaces for different event types
  io.of("/orders").on("connection", (socket) => {
    console.log(`[Orders Namespace] Client connected: ${socket.id}`);

    // Store user info when they join
    socket.on("user-join", (data) => {
      socket.data.userId = data.userId;
      socket.data.role = data.role;

      if (data.role === "admin") {
        socket.join("admin");
      } else {
        socket.join(`customer-${data.userId}`);
      }

      console.log(`[Orders] User ${data.userId} (${data.role}) joined`);
    });

    socket.on("disconnect", () => {
      console.log(`[Orders Namespace] Client disconnected: ${socket.id}`);
    });
  });

  // Make io available globally for API routes
  global.io = io;

  server.listen(PORT, (err) => {
    if (err) throw err;
    console.log(`> Ready on http://localhost:${PORT}`);
    console.log(`> Socket.IO server initialized`);
  });
});
