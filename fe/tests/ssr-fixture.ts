import { randomUUID } from 'node:crypto'
import { spawn } from 'node:child_process'
import { createServer } from 'node:http'
import { once } from 'node:events'
import { resolve } from 'node:path'
import { test as base, expect, type Page } from '@playwright/test'

type Session = {
  id: string
  email: string
  displayName: string
  isSuperAdmin: boolean
}
type SessionReply = { user: Session | null; status?: number; offline?: boolean }
type Resolver = () => SessionReply | Promise<SessionReply>
const pageSessions = new WeakMap<Page, { resolve: Resolver }>()

// Le intercettazioni page.route coprono solo il browser. Questa fixture offre
// un'API HTTP reale al processo SSR, con identità separata per ogni test.
export function setSsrSession(page: Page, resolver: Resolver) {
  const session = pageSessions.get(page)
  if (!session) throw new Error('Usare la fixture SSR per questa pagina.')
  session.resolve = resolver
}

type AppServer = {
  baseURL: string
  sessions: Map<string, { resolve: Resolver }>
}

export const test = base.extend<object, { appServer: AppServer }>({
  appServer: [
    // Playwright richiede il destructuring anche quando non servono fixture.
    // eslint-disable-next-line no-empty-pattern
    async ({}, use, workerInfo) => {
      const sessions: AppServer['sessions'] = new Map()
      const api = createServer(async (request, response) => {
        if (request.url !== '/api/Auth/Me') {
          response.writeHead(404).end()
          return
        }
        const key = /(?:^|;\s*)Fantastiche\.Auth=([^;]+)/.exec(
          request.headers.cookie ?? '',
        )?.[1]
        const session = key ? sessions.get(key) : undefined
        const reply = session ? await session.resolve() : { user: null }
        if (reply.offline) {
          request.socket.destroy()
          return
        }
        const status = reply.status ?? (reply.user ? 200 : 401)
        response.writeHead(status, { 'Content-Type': 'application/json' })
        response.end(
          JSON.stringify({
            isSuccess: status === 200,
            data: reply.user,
            errors: [],
          }),
        )
      })
      api.listen(0, '127.0.0.1')
      await once(api, 'listening')
      const address = api.address()
      if (!address || typeof address === 'string') throw new Error('Porta API')
      const port = 6261 + workerInfo.parallelIndex
      const baseURL = `http://localhost:${port}`
      const frontendDir = resolve(import.meta.dirname, '..')
      const preview = process.env.SSR_E2E_PREVIEW === '1'
      const child = spawn(
        process.execPath,
        preview
          ? ['server/index.mjs']
          : ['node_modules/vite/bin/vite.js', 'dev', '--port', String(port)],
        {
          cwd: frontendDir,
          env: {
            ...process.env,
            API_UPSTREAM: `http://127.0.0.1:${address.port}`,
            VITE_API_BASE_URL: 'http://localhost:6060',
            VITE_CACHE_DIR: `node_modules/.vite-e2e-${workerInfo.parallelIndex}`,
            HOST: '127.0.0.1',
            PORT: String(port),
          },
          stdio: ['ignore', 'pipe', 'pipe'],
        },
      )
      let output = ''
      child.stdout.on('data', (data: Buffer) => (output += data.toString()))
      child.stderr.on('data', (data: Buffer) => (output += data.toString()))
      try {
        await expect
          .poll(
            async () => {
              if (child.exitCode !== null) throw new Error(output)
              return fetch(`${baseURL}/login`)
                .then((response) => response.status)
                .catch(() => 0)
            },
            { timeout: 30_000 },
          )
          .toBe(200)
        await use({ baseURL, sessions })
      } finally {
        if (child.exitCode === null && child.signalCode === null) {
          const exited = once(child, 'exit')
          child.kill('SIGTERM')
          await exited
        }
        api.closeAllConnections()
        await new Promise<void>((done) => api.close(() => done()))
      }
    },
    { scope: 'worker' },
  ],
  baseURL: async ({ appServer }, use) => use(appServer.baseURL),
  page: async ({ page, appServer }, use) => {
    const key = `e2e-${randomUUID()}`
    const session = { resolve: (() => ({ user: null })) as Resolver }
    appServer.sessions.set(key, session)
    pageSessions.set(page, session)
    await page.context().addCookies([
      {
        name: 'Fantastiche.Auth',
        value: key,
        url: appServer.baseURL,
        httpOnly: true,
      },
    ])
    try {
      await use(page)
    } finally {
      appServer.sessions.delete(key)
      pageSessions.delete(page)
    }
  },
})

export { expect }
export type { Page, WebSocketRoute } from '@playwright/test'
