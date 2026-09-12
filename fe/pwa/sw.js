// La build inserisce versione e lista esatta degli asset pubblici emessi da Vite.
/* global __PWA_CONFIG__ */
const config = __PWA_CONFIG__
const cacheName = `fantastiche-static-${config.version}`
const offlineFiles = ['/offline.html', '/pwa/icon-192.png', config.font]
const assets = new Set(config.assets)

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(cacheName).then((cache) => cache.addAll(offlineFiles)),
  )
  // Nessuno skipWaiting: tutte le finestre devono chiudersi prima dell'update.
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    (async () => {
      const keys = await caches.keys()
      await Promise.all(
        keys
          .filter(
            (key) => key.startsWith('fantastiche-static-') && key !== cacheName,
          )
          .map((key) => caches.delete(key)),
      )
      await self.clients.claim()
    })(),
  )
})

self.addEventListener('fetch', (event) => {
  const request = event.request
  const url = new URL(request.url)
  if (request.method !== 'GET' || url.origin !== self.location.origin) return
  if (/^\/(api|hubs|_server)(\/|$)/i.test(url.pathname)) return

  if (request.mode === 'navigate') {
    // Le pagine, anche login e inviti, passano sempre dalla rete. Mai cache HTML.
    event.respondWith(
      fetch(request).catch(async () => {
        const cache = await caches.open(cacheName)
        return (await cache.match('/offline.html')) ?? Response.error()
      }),
    )
    return
  }
  // Solo URL esatti della build: niente dati API, foto remote o query private.
  if (
    url.search ||
    (!assets.has(url.pathname) && !offlineFiles.includes(url.pathname))
  )
    return
  event.respondWith(
    (async () => {
      const cache = await caches.open(cacheName)
      const cached = await cache.match(request)
      if (cached) return cached
      const response = await fetch(request)
      if (
        response.ok &&
        !response.redirected &&
        response.type === 'basic' &&
        !/no-store|private/i.test(
          response.headers.get('Cache-Control') ?? '',
        ) &&
        !/text\/html/i.test(response.headers.get('Content-Type') ?? '')
      ) {
        try {
          await cache.put(request, response.clone())
        } catch {
          /* Quota: usare la rete. */
        }
      }
      return response
    })(),
  )
})
