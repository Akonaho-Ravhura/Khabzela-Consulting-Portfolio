// Service worker for the installable app (PWA).
// Pages always come from the network so clients never see stale tax or invoice data; if the
// network is down, an offline page is shown. Static files (CSS, JS, icons) are cached.
const CACHE = "khabzela-v1";
const OFFLINE_URL = "/Home/Offline";
const PRECACHE = [OFFLINE_URL, "/css/site.css", "/css/layouts/public.css", "/js/site.js", "/icons/icon-192.png"];

self.addEventListener("install", (event) => {
    event.waitUntil(caches.open(CACHE).then((cache) => cache.addAll(PRECACHE)).then(() => self.skipWaiting()));
});

self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(keys.filter((k) => k !== CACHE).map((k) => caches.delete(k))))
            .then(() => self.clients.claim())
    );
});

self.addEventListener("fetch", (event) => {
    const request = event.request;
    if (request.method !== "GET" || new URL(request.url).origin !== self.location.origin) return;

    if (request.mode === "navigate") {
        event.respondWith(fetch(request).catch(() => caches.match(OFFLINE_URL)));
        return;
    }

    if (/^\/(css|js|icons|lib)\//.test(new URL(request.url).pathname)) {
        // Stale-while-revalidate for static assets.
        event.respondWith(
            caches.open(CACHE).then((cache) =>
                cache.match(request).then((cached) => {
                    const network = fetch(request).then((response) => {
                        if (response.ok) cache.put(request, response.clone());
                        return response;
                    }).catch(() => cached);
                    return cached || network;
                })
            )
        );
    }
});
