import { setSsrSession } from '../ssr-fixture'
import { expect, test, type Page } from '../ssr-fixture'

const admin = {
  id: 'catalog-admin',
  email: 'admin@example.test',
  displayName: 'Admin',
  isSuperAdmin: true,
}
const versionId = '00000000-0000-4000-8000-000000000002'
const version = {
  id: versionId,
  seasonName: '2026/27',
  status: 'Draft',
  source: 'FantacalcioCsv',
  contentHash: 'test-hash',
  entryCount: 1,
  createdAt: '2026-09-10T10:00:00Z',
  publishedAt: null as string | null,
}
const csv =
  '1,Rossi,Mario Rossi,P,Por,1,1,1,1,Roma,1,1,destro,Italia,01/01/2000,https://example.test/player.png,0,0,0\n'
const player = {
  playerId: 'player',
  externalId: '1',
  name: 'Rossi',
  fullName: 'Mario Rossi',
  role: 'P',
  clubName: 'Roma',
  birthDate: '2000-01-01T00:00:00',
  nationality: 'Italia',
  preferredFoot: 'destro',
  photoUrl: null,
}
async function setup(page: Page, isSuperAdmin = true) {
  const state = {
    imports: [] as unknown[],
    publishes: 0,
    imported: false,
    published: false,
  }
  setSsrSession(page, () => ({ user: { ...admin, isSuperAdmin } }))
  await page.route('http://localhost:6060/api/**', async (route) => {
    const req = route.request()
    const url = new URL(req.url())
    const data = {
      ...version,
      status: state.published ? 'Published' : 'Draft',
      publishedAt: state.published ? '2026-09-10T11:00:00Z' : null,
    }
    const respond = (body: unknown) =>
      route.fulfill({ json: { isSuccess: true, data: body, errors: [] } })
    if (url.pathname.endsWith('/Auth/Me'))
      return respond({ ...admin, isSuperAdmin })
    if (url.pathname.endsWith('/Auth/Antiforgery'))
      return respond({ token: 'catalog-csrf' })
    if (url.pathname.endsWith('/Catalog/Imports')) {
      expect(req.headers()['x-xsrf-token']).toBe('catalog-csrf')
      state.imports.push(req.postDataJSON())
      state.imported = true
      return respond(data)
    }
    if (url.pathname.endsWith('/Publish')) {
      expect(req.headers()['x-xsrf-token']).toBe('catalog-csrf')
      state.publishes++
      state.published = true
      return respond({
        ...data,
        status: 'Published',
        publishedAt: '2026-09-10T11:00:00Z',
      })
    }
    if (url.pathname.endsWith('/Catalog/Versions'))
      return respond({
        items: state.imported ? [data] : [],
        page: 1,
        pageSize: 10,
        total: state.imported ? 1 : 0,
      })
    if (url.pathname.endsWith('/Entries'))
      return respond({ items: [player], page: 1, pageSize: 30, total: 1 })
    if (url.pathname === '/api/Leagues')
      return respond({ items: [], page: 1, pageSize: 20, totalCount: 0 })
    return route.fulfill({ status: 404 })
  })
  return state
}
for (const viewport of [
  { width: 1280, height: 900 },
  { width: 390, height: 844 },
]) {
  test(`importazione, anteprima e pubblicazione esplicita a ${viewport.width}px`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport)
    const state = await setup(page)
    await page.goto('/catalogo')
    await page.getByLabel('Stagione del listone').fill('2026/27')
    await page.getByLabel('File CSV Fantacalcio').setInputFiles({
      name: 'sintetico-test.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csv),
    })
    await page.getByRole('button', { name: 'Importa in bozza' }).click()
    await expect(
      page.getByText('Listone importato. Puoi consultarlo nell’anteprima.'),
    ).toBeVisible()
    expect(state.imports).toEqual([{ seasonName: '2026/27', csv }])
    expect(state.publishes).toBe(0)
    await expect(page.getByText('Rossi', { exact: true })).toBeVisible()
    await page.getByLabel('Cerca calciatore').fill('Rossi')
    await page.getByLabel('Ruolo', { exact: true }).selectOption('P')
    await page.getByLabel('Club', { exact: true }).fill('Roma')
    const filtered = page.waitForRequest((req) =>
      req.url().includes('/Entries?search=Rossi&role=P&club=Roma&page=1'),
    )
    await page.getByRole('button', { name: 'Filtra', exact: true }).click()
    await filtered
    await page.getByRole('button', { name: 'Pubblica listone' }).click()
    await expect(page.getByText(/Questo listone è pubblicato/)).toBeVisible()
    expect(state.publishes).toBe(1)
    await expect(
      page.getByRole('button', { name: 'Pubblica listone' }),
    ).toHaveCount(0)
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true)
    await page.screenshot({
      path: `/tmp/fantastiche-catalog-${viewport.width}.png`,
      fullPage: true,
    })
  })
}
test('il membro ordinario viene reindirizzato dalle funzioni di amministrazione', async ({
  page,
}) => {
  const state = await setup(page, false)
  await page.goto('/catalogo')
  await expect(page).toHaveURL(/\/leghe(?:\?|$)/)
  expect(state.imports).toHaveLength(0)
  expect(state.publishes).toBe(0)
})
