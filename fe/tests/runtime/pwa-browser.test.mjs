// Build reale dietro un proxy di test: nessuna sessione o asta del database.
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { once } from 'node:events'
import { readFile } from 'node:fs/promises'
import { createServer } from 'node:http'
import { test } from 'node:test'
import { chromium, expect } from '@playwright/test'

async function listen(server) {
  server.listen(0, '127.0.0.1')
  await once(server, 'listening')
  return server.address().port
}

test(
  'PWA di produzione: installabilità, offline privato, rete e aggiornamento con due finestre',
  { timeout: 60_000 },
  async () => {
    const reservation = createServer()
    const runtimePort = await listen(reservation)
    await new Promise((resolve) => reservation.close(resolve))
    const child = spawn(process.execPath, ['server/index.mjs'], {
      cwd: new URL('../../', import.meta.url),
      env: { ...process.env, PORT: String(runtimePort) },
      stdio: 'pipe',
    })
    let logs = ''
    child.stderr.on('data', (data) => {
      logs += data
    })
    child.stdout.on('data', (data) => {
      logs += data
    })
    const runtime = `http://127.0.0.1:${runtimePort}`
    let version = null
    let writes = 0
    const sw = await readFile(
      new URL('../../dist/client/sw.js', import.meta.url),
      'utf8',
    )
    const proxy = createServer(async (request, response) => {
      try {
        if (request.url === '/sw.js') {
          response.writeHead(200, {
            'Content-Type': 'application/javascript',
            'Cache-Control': 'no-cache',
          })
          response.end(
            version
              ? sw.replace(/"version":"[^"]+"/, `"version":"${version}"`)
              : sw,
          )
          return
        }
        if (request.url.startsWith('/api/pwa-probe')) {
          if (request.method === 'POST') writes++
          response.writeHead(200, {
            'Content-Type': 'application/json',
            'Cache-Control': 'private, no-store',
          })
          response.end(JSON.stringify({ privateData: 'dato di test', writes }))
          return
        }
        if (request.url === '/leghe/test/asta') {
          response.writeHead(200, {
            'Content-Type': 'text/html',
            'Cache-Control': 'private, no-store',
          })
          response.end(
            '<html><title>Asta di test</title><body>Dati privati di test</body></html>',
          )
          return
        }
        const upstream = await fetch(`${runtime}${request.url}`, {
          redirect: 'manual',
        })
        const headers = Object.fromEntries(upstream.headers)
        // fetch decodifica eventuale compressione prima di inoltrare il body.
        delete headers['content-encoding']
        delete headers['content-length']
        response.writeHead(upstream.status, headers)
        response.end(Buffer.from(await upstream.arrayBuffer()))
      } catch {
        response.writeHead(502).end()
      }
    })
    let browser
    try {
      await expect
        .poll(
          async () =>
            fetch(`${runtime}/healthz`)
              .then((r) => r.ok)
              .catch(() => false),
          { timeout: 15000, message: logs },
        )
        .toBe(true)
      const origin = `http://127.0.0.1:${await listen(proxy)}`
      // Un profilo temporaneo normale permette la verifica di installabilità.
      browser = await chromium.launchPersistentContext('', {
        channel: 'chrome',
      })
      const context = browser
      await Promise.all(context.pages().map((page) => page.close()))
      let page = await context.newPage()
      const errors = []
      page.on('pageerror', (error) => errors.push(error.message))
      await page.goto(`${origin}/login`)
      await expect(page.getByLabel('Email', { exact: true })).toBeEnabled()
      await expect
        .poll(() => page.evaluate(() => !!navigator.serviceWorker.controller))
        .toBe(true)
      await page.reload()
      await expect(page.getByLabel('Email', { exact: true })).toBeEnabled()
      const cdp = await context.newCDPSession(page)
      const manifest = await cdp.send('Page.getAppManifest')
      assert.deepEqual(manifest.errors, [])
      assert.equal(JSON.parse(manifest.data).display, 'standalone')
      assert.deepEqual(
        (await cdp.send('Page.getInstallabilityErrors')).installabilityErrors,
        [],
      )
      for (const width of [390, 1440]) {
        await page.setViewportSize({ width, height: 900 })
        await page.screenshot({ path: `/tmp/fantastiche-pwa-${width}.png` })
      }
      await page.evaluate(() => fetch('/api/pwa-probe', { method: 'POST' }))
      assert.equal(writes, 1)
      const auction = await context.newPage()
      await auction.goto(`${origin}/leghe/test/asta`)
      await auction.evaluate(() => {
        window.pwaTestMarker = 42
      })
      const cacheUrls = () =>
        page.evaluate(async () => {
          const all = await Promise.all(
            (await caches.keys()).map(async (key) =>
              (await (await caches.open(key)).keys()).map(
                (r) => new URL(r.url).pathname,
              ),
            ),
          )
          return all.flat()
        })
      const cached = await cacheUrls()
      assert(cached.includes('/offline.html'))
      assert(cached.some((url) => url.startsWith('/assets/')))
      assert(
        cached.every(
          (url) =>
            url === '/offline.html' ||
            url === '/pwa/icon-192.png' ||
            url.startsWith('/assets/'),
        ),
      )
      const originalCaches = await page.evaluate(() => caches.keys())
      version = 'browser-update-test'
      await page.evaluate(async () =>
        (await navigator.serviceWorker.getRegistration()).update(),
      )
      await expect
        .poll(() =>
          page.evaluate(
            async () =>
              !!(await navigator.serviceWorker.getRegistration()).waiting,
          ),
        )
        .toBe(true)
      await expect(page.getByRole('status')).toContainText(
        'Nuova versione pronta',
      )
      await page.reload()
      assert.equal(await auction.evaluate(() => window.pwaTestMarker), 42)
      assert(
        await page.evaluate(
          async () =>
            !!(await navigator.serviceWorker.getRegistration()).waiting,
        ),
      )
      assert(
        (await page.evaluate(() => caches.keys())).includes(originalCaches[0]),
      )
      await context.setOffline(true)
      assert.equal(
        await page.evaluate(async () =>
          fetch('/api/pwa-probe', { method: 'POST' })
            .then(() => true)
            .catch(() => false),
        ),
        false,
      )
      await auction.reload()
      await expect(auction.getByRole('heading')).toContainText(
        'Torniamo in campo',
      )
      await expect(auction.locator('body')).not.toContainText('Dati privati')
      await context.setOffline(false)
      await auction.getByRole('link', { name: 'Riprova' }).click()
      await expect(auction.locator('body')).toContainText(
        'Dati privati di test',
      )
      assert.equal(writes, 1, 'nessun replay della richiesta fallita offline')
      await page.close()
      await auction.close()
      const newWorker = context.serviceWorkers().at(-1)
      await expect
        .poll(() =>
          newWorker.evaluate(() => self.registration.waiting === null),
        )
        .toBe(true)
      page = await context.newPage()
      await page.goto(`${origin}/login`)
      await expect(page.getByLabel('Email', { exact: true })).toBeEnabled()
      await expect
        .poll(() => page.evaluate(() => caches.keys()))
        .toEqual(['fantastiche-static-browser-update-test'])
      assert.deepEqual(errors, [])
    } finally {
      await browser?.close()
      proxy.closeAllConnections()
      await new Promise((resolve) => proxy.close(resolve))
      if (child.exitCode === null && child.signalCode === null) {
        child.kill('SIGTERM')
        await once(child, 'exit')
      }
    }
  },
)
