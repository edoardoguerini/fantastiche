// Eseguire dopo `just fe build-image`. Nessun accesso al database applicativo.
import assert from 'node:assert/strict'
import { execFile } from 'node:child_process'
import { createHash, randomUUID } from 'node:crypto'
import { once } from 'node:events'
import { createServer } from 'node:http'
import { test } from 'node:test'
import { promisify } from 'node:util'

const exec = promisify(execFile)
const user = {
  id: 'smoke',
  email: 'smoke@example.test',
  displayName: 'Smoke',
  isSuperAdmin: false,
}

test(
  'nginx e Node: HTML SSR, cookie, proxy API e WebSocket',
  { timeout: 60_000 },
  async () => {
    const cookies = []
    const sockets = new Set()
    const api = createServer((request, response) => {
      const valid = request.headers.cookie?.includes('Fantastiche.Auth=valid')
      const me = request.url === '/api/Auth/Me'
      const status = me && !valid ? 401 : 200
      if (me) cookies.push(request.headers.cookie)
      response.writeHead(status, {
        'Content-Type': 'application/json',
        'Set-Cookie': valid
          ? 'Fantastiche.Auth=valid; Path=/; HttpOnly; SameSite=Lax'
          : 'Fantastiche.Auth=; Path=/; Max-Age=0; HttpOnly',
      })
      response.end(
        JSON.stringify({
          isSuccess: status === 200,
          data: me ? (valid ? user : null) : { token: 'csrf' },
          errors: [],
        }),
      )
    })
    api.on('upgrade', (request, socket) => {
      sockets.add(socket)
      socket.on('close', () => sockets.delete(socket))
      const accept = createHash('sha1')
        .update(
          request.headers['sec-websocket-key'] +
            '258EAFA5-E914-47DA-95CA-C5AB0DC85B11',
        )
        .digest('base64')
      socket.write(
        `HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: ${accept}\r\n\r\n`,
      )
      const payload = Buffer.from(request.url)
      socket.write(
        Buffer.concat([Buffer.from([0x81, payload.length]), payload]),
      )
    })
    api.listen(0, '0.0.0.0')
    await once(api, 'listening')
    const name = `fantastiche-ssr-smoke-${randomUUID()}`
    let started = false
    try {
      await exec('docker', [
        'run',
        '--rm',
        '-d',
        '--name',
        name,
        '-p',
        '127.0.0.1::8080',
        '-e',
        `API_UPSTREAM=http://host.docker.internal:${api.address().port}`,
        process.env.SSR_TEST_IMAGE ?? 'fantastiche-fe:local',
      ])
      started = true
      const { stdout } = await exec('docker', ['port', name, '8080'])
      const origin = `http://${stdout.trim()}`
      await assert.doesNotReject(async () => {
        for (let attempt = 0; attempt < 40; attempt++) {
          const healthy = await fetch(`${origin}/healthz`)
            .then((r) => r.ok)
            .catch(() => false)
          if (healthy) return
          await new Promise((done) => setTimeout(done, 250))
        }
        throw new Error('Runtime non pronto')
      })

      const login = await fetch(`${origin}/login`)
      const html = await login.text()
      assert.equal(login.status, 200)
      assert.match(login.headers.get('cache-control'), /private, no-store/)
      assert.match(html, /<form/)
      assert.match(html, /type="email"/)
      assert.doesNotMatch(html, /Caricamento in corso/)
      assert.deepEqual(cookies, [])

      assert.match(html, /rel="manifest"/)
      const manifest = await fetch(`${origin}/manifest.webmanifest`)
      assert.equal(manifest.status, 200)
      assert.match(
        manifest.headers.get('content-type'),
        /application\/manifest\+json/,
      )
      assert.match(manifest.headers.get('cache-control'), /no-cache/)
      assert.equal((await manifest.json()).start_url, '/leghe')
      const worker = await fetch(`${origin}/sw.js`)
      assert.equal(worker.status, 200)
      assert.match(worker.headers.get('content-type'), /javascript/)
      assert.match(worker.headers.get('cache-control'), /no-cache/)
      assert.match(await worker.text(), /const config = \{"version":/)
      const offline = await fetch(`${origin}/offline.html`)
      assert.equal(offline.status, 200)
      assert.match(await offline.text(), /Torniamo in campo/)

      const authenticated = await fetch(`${origin}/login`, {
        headers: { Cookie: 'Fantastiche.Auth=valid; unrelated=private' },
        redirect: 'manual',
      })
      assert.equal(authenticated.status, 307)
      assert.match(authenticated.headers.get('location'), /^\/leghe/)
      assert.match(authenticated.headers.get('cache-control'), /no-store/)
      assert.match(
        authenticated.headers.get('set-cookie'),
        /Fantastiche.Auth=valid/,
      )
      assert.deepEqual(cookies, ['Fantastiche.Auth=valid'])

      const expired = await fetch(`${origin}/login`, {
        headers: { Cookie: 'Fantastiche.Auth=expired' },
      })
      assert.equal(expired.status, 200)
      assert.match(expired.headers.get('set-cookie'), /Max-Age=0/)

      const asset = /(?:href|src)="(\/assets\/[^"]+)"/.exec(html)?.[1]
      assert.ok(asset)
      const resource = await fetch(`${origin}${asset}`)
      assert.equal(resource.status, 200)
      assert.match(resource.headers.get('cache-control'), /immutable/)

      const apiResponse = await fetch(`${origin}/api/Auth/Antiforgery`)
      assert.equal(apiResponse.status, 200)
      assert.equal((await apiResponse.json()).data.token, 'csrf')
      const ws = new WebSocket(
        `${origin.replace('http:', 'ws:')}/hubs/Auctions?id=smoke`,
      )
      const message = await new Promise((done, reject) => {
        ws.addEventListener('message', (event) => done(event.data), {
          once: true,
        })
        ws.addEventListener('error', reject, { once: true })
      })
      assert.equal(message, '/hubs/Auctions?id=smoke')
      ws.close()
    } finally {
      if (started) await exec('docker', ['stop', '-t', '2', name])
      for (const socket of sockets) socket.destroy()
      api.closeAllConnections()
      await new Promise((done) => api.close(done))
    }
  },
)
