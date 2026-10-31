/* Leikpyar Hunters - the service worker. It keeps the big game files (/play/Build/...) on the device, so the next visit starts at once. */
const CACHE = "lh-game";

self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (e) => e.waitUntil(self.clients.claim()));

function stored(res) {
  // the body is already decoded here: only the type is kept
  return new Response(res.body, { status: 200, headers: { "Content-Type": res.headers.get("Content-Type") || "application/octet-stream" } });
}

self.addEventListener("fetch", (e) => {
  const req = e.request;
  if (req.method !== "GET") return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin || !url.pathname.startsWith("/play/Build/")) return;
  e.respondWith((async () => {
    const cache = await caches.open(CACHE);
    const hit = await cache.match(req.url);
    if (hit) return hit;
    const res = await fetch(req);
    if (res.ok && res.status === 200) cache.put(req.url, stored(res.clone())).catch(() => {});
    return res;
  })());
});

async function tell(msg) {
  const all = await self.clients.matchAll({ includeUncontrolled: true });
  all.forEach((c) => c.postMessage(msg));
}

async function prepare(files) {
  const cache = await caches.open(CACHE);
  const total = files.reduce((n, f) => n + (f.size || 0), 0);
  let done = 0;
  try {
    for (const f of files) {
      if (await cache.match(f.url)) { done += f.size || 0; await tell({ type: "progress", done, total }); continue; }
      const res = await fetch(f.url);
      if (!res.ok || !res.body) throw new Error("HTTP " + res.status);
      const [a, b] = res.body.tee();
      const putting = cache.put(f.url, new Response(b, { status: 200, headers: { "Content-Type": res.headers.get("Content-Type") || "application/octet-stream" } }));
      const reader = a.getReader();
      let last = 0;
      for (;;) {
        const { done: end, value } = await reader.read();
        if (end) break;
        done += value.length;
        const now = Date.now();
        if (now - last > 200) { last = now; await tell({ type: "progress", done, total }); }
      }
      await putting;
    }
    // files of an older version are removed
    const keep = new Set(files.map((f) => new URL(f.url, self.location.origin).href));
    for (const req of await cache.keys()) if (!keep.has(req.url)) await cache.delete(req);
    await tell({ type: "progress", done: total, total });
    await tell({ type: "done" });
  } catch (err) {
    await tell({ type: "error", message: String((err && err.message) || err) });
  }
}

self.addEventListener("message", (e) => {
  const d = e.data || {};
  if (d.type === "prepare" && Array.isArray(d.files)) e.waitUntil(prepare(d.files));
  else if (d.type === "clear") e.waitUntil(caches.delete(CACHE).then(() => tell({ type: "cleared" })));
});
