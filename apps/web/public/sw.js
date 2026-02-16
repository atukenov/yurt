// Yurt Coffee — Service Worker
// Provides offline shell caching + runtime network-first strategy

const CACHE_NAME = "yurt-v1";
const OFFLINE_URL = "/offline.html";

// Assets to pre-cache during install (app shell)
const PRE_CACHE = [
  "/",
  "/manifest.json",
  "/images/icons/icon-192x192.png",
  "/images/icons/icon-512x512.png",
  "/images/logo.png",
  OFFLINE_URL,
];

// ── Install ───────────────────────────────────────────────
self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(CACHE_NAME)
      .then((cache) => cache.addAll(PRE_CACHE))
      .then(() => self.skipWaiting())
  );
});

// ── Activate — clean old caches ───────────────────────────
self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(
          keys
            .filter((key) => key !== CACHE_NAME)
            .map((key) => caches.delete(key))
        )
      )
      .then(() => self.clients.claim())
  );
});

// ── Fetch — network-first with cache fallback ─────────────
self.addEventListener("fetch", (event) => {
  const { request } = event;

  // Only handle GET requests
  if (request.method !== "GET") return;

  // Skip cross-origin & API calls — let the browser handle them
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (url.pathname.startsWith("/api/")) return;

  event.respondWith(
    fetch(request)
      .then((response) => {
        // Cache successful responses for static assets
        if (
          response.ok &&
          (request.destination === "image" ||
            request.destination === "style" ||
            request.destination === "script" ||
            request.destination === "font")
        ) {
          const clone = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(request, clone));
        }
        return response;
      })
      .catch(() => {
        // Network failed — try cache
        return caches.match(request).then((cached) => {
          if (cached) return cached;
          // For navigation requests show offline page
          if (request.mode === "navigate") {
            return caches.match(OFFLINE_URL);
          }
          return new Response("Offline", { status: 503 });
        });
      })
  );
});
