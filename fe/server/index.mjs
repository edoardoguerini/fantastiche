// Runtime HTTP di TanStack Start. In produzione nginx serve statici e proxy;
// il middleware statico permette anche la preview locale della stessa build.
import { fileURLToPath } from 'node:url'
import { serve } from 'srvx'
import { serveStatic } from 'srvx/static'
import handler from '../dist/server/server.js'

const staticFiles = serveStatic({
  dir: fileURLToPath(new URL('../dist/client', import.meta.url)),
})
const server = serve({
  hostname: process.env.HOST ?? '127.0.0.1',
  port: Number(process.env.PORT ?? 6061),
  middleware: [
    async (request, next) => {
      const response = await staticFiles(request, () => undefined)
      if (!response) return next()
      response.headers.set(
        'Cache-Control',
        new URL(request.url).pathname.startsWith('/assets/')
          ? 'public, max-age=31536000, immutable'
          : 'no-cache',
      )
      return response
    },
  ],
  fetch: async (request) => {
    if (new URL(request.url).pathname === '/healthz')
      return new Response('ok', { headers: { 'Cache-Control': 'no-store' } })
    const response = await handler.fetch(request)
    response.headers.set('Cache-Control', 'private, no-store')
    return response
  },
  error: (error) => {
    console.error(error)
    return new Response('Errore del server', {
      status: 500,
      headers: { 'Cache-Control': 'no-store' },
    })
  },
})

await server.ready()
