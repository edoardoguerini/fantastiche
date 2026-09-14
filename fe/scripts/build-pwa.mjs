import { createHash } from 'node:crypto'
import { readFile, readdir, writeFile } from 'node:fs/promises'

const client = new URL('../dist/client/', import.meta.url)
const files = (await readdir(new URL('assets/', client), { recursive: true }))
  .filter((name) => /\.(js|css|woff2?|png|webp|svg|m4a|mp3)$/.test(name))
  .sort()
const font = files.find((name) => name.startsWith('inter-latin-wght-normal-'))
if (!font) throw new Error('Font Inter mancante nella build PWA')
const fontPath = `/assets/${font}`
const offline = await readFile(
  new URL('../public/offline.html', import.meta.url),
  'utf8',
)
await writeFile(
  new URL('offline.html', client),
  offline.replace('__PWA_FONT__', fontPath),
)
const template = await readFile(
  new URL('../pwa/sw.js', import.meta.url),
  'utf8',
)
const hash = createHash('sha256').update(template)
for (const file of [
  'offline.html',
  'manifest.webmanifest',
  'pwa/icon-192.png',
  ...files.map((name) => `assets/${name}`),
]) {
  hash.update(file).update(await readFile(new URL(file, client)))
}
const config = {
  version: hash.digest('hex').slice(0, 16),
  font: fontPath,
  assets: files.map((name) => `/assets/${name}`),
}
await writeFile(
  new URL('sw.js', client),
  template.replace(
    'const config = __PWA_CONFIG__',
    `const config = ${JSON.stringify(config)}`,
  ),
)
console.log(
  `PWA ${config.version}: ${files.length} asset pubblici, offline pronto.`,
)
