// Yurt POS offline helper: keeps the till's own files on the tablet so the app opens without internet.
// It never touches server calls (/api, other sites): sales go through the app's own upload queue.
const CACHE = 'yurt-pos-v1';

// Saves index.html and every file it links to (the app's scripts and styles, whose names change with each release).
async function saveShell() {
  const cache = await caches.open(CACHE);
  const res = await fetch('index.html', { cache: 'no-store' });
  if (!res.ok) return;
  const html = await res.clone().text();
  await cache.put('index.html', res);
  const files = [...html.matchAll(/(?:src|href)="([^"]+)"/g)]
    .map((m) => m[1])
    .filter((u) => !/^(https?:)?\/\//.test(u) && !u.startsWith('data:'));
  await Promise.all(files.map((u) => cache.add(u).catch(() => undefined)));
  // Parts the scripts load later (chunk-XXXX.js) are saved too.
  const seen = new Set();
  const todo = files.filter((f) => f.endsWith('.js'));
  while (todo.length) {
    const hit = await cache.match(todo.pop());
    if (!hit) continue;
    for (const [name] of (await hit.text()).matchAll(/chunk-[A-Z0-9]+\.js/g)) {
      if (seen.has(name)) continue;
      seen.add(name);
      if (await cache.add(name).then(() => true, () => false)) todo.push(name);
    }
  }
}

self.addEventListener('install', (event) => {
  event.waitUntil(saveShell().catch(() => undefined).then(() => self.skipWaiting()));
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin || url.pathname.includes('/api/')) return;

  if (req.mode === 'navigate') {
    // The page: fresh from the internet when there is one (and saved again), else the saved one.
    event.respondWith(
      fetch(req)
        .then((res) => {
          if (res.ok) event.waitUntil(saveShell().catch(() => undefined));
          return res;
        })
        .catch(async () => (await caches.match('index.html')) ?? Response.error()),
    );
    return;
  }

  // Scripts, styles, pictures: saved copy first (their names change when they change), else fetch and save.
  event.respondWith(
    caches.match(req).then((hit) => hit ?? fetch(req).then((res) => {
      if (res.ok && res.type === 'basic') {
        const copy = res.clone();
        event.waitUntil(caches.open(CACHE).then((c) => c.put(req, copy)));
      }
      return res;
    })),
  );
});
