import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import { test } from 'node:test'
import { runInNewContext } from 'node:vm'

const source = await readFile(
  new URL('../../pwa/sw.js', import.meta.url),
  'utf8',
)
function worker() {
  const handlers = {}
  runInNewContext(
    source.replace(
      'const config = __PWA_CONFIG__',
      'const config = ' +
        JSON.stringify({
          version: 'test',
          assets: ['/assets/app-hash.js'],
        }),
    ),
    {
      self: {
        location: { origin: 'https://fantastiche.test' },
        addEventListener: (name, fn) => {
          handlers[name] = fn
        },
      },
      URL,
      Set,
    },
  )
  return handlers
}

for (const [url, method, mode] of [
  ['/api/Auth/Me', 'GET', 'cors'],
  ['/api/Offers', 'POST', 'cors'],
  ['/hubs/Auctions/negotiate', 'POST', 'cors'],
  ['/_server/fn', 'GET', 'cors'],
  ['/leghe', 'GET', 'cors'],
  ['/assets/private.json', 'GET', 'cors'],
  ['https://other.test/assets/app-hash.js', 'GET', 'cors'],
  ['/assets/app-hash.js?token=private', 'GET', 'cors'],
]) {
  test(`nessuna intercettazione o cache: ${method} ${url}`, () => {
    let intercepted = false
    worker().fetch({
      request: {
        url: new URL(url, 'https://fantastiche.test').href,
        method,
        mode,
      },
      respondWith: () => {
        intercepted = true
      },
    })
    assert.equal(intercepted, false)
  })
}
