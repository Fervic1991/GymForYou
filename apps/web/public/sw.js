const CACHE_NAME = 'gym-saas-static-v2';
const STATIC_ASSETS = [
  '/manifest.json',
  '/icons/icon-192.svg',
  '/icons/icon-512.svg'
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(STATIC_ASSETS))
  );
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k)))
    )
  );
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') return;

  const url = new URL(event.request.url);
  const isHttp = url.protocol === 'http:' || url.protocol === 'https:';
  const sameOrigin = url.origin === self.location.origin;
  if (!isHttp || !sameOrigin) return;

  // Never cache Next.js chunks/pages: avoid stale chunk errors after deploy.
  if (url.pathname.startsWith('/_next/')) return;
  if (event.request.mode === 'navigate') return;

  const isStaticPwaAsset =
    STATIC_ASSETS.includes(url.pathname) ||
    url.pathname.startsWith('/icons/');

  if (!isStaticPwaAsset) return;

  event.respondWith(
    caches.match(event.request).then((cached) => {
      if (cached) return cached;
      return fetch(event.request).then((response) => {
        if (response && response.status === 200) {
          const clone = response.clone();
          caches.open(CACHE_NAME).then((cache) => cache.put(event.request, clone));
        }
        return response;
      });
    })
  );
});
