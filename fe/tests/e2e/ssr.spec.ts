import { test, expect, setSsrSession } from '../ssr-fixture'

const user = {
  id: 'ssr-user',
  email: 'ssr@example.test',
  displayName: 'Utente SSR',
  isSuperAdmin: false,
}

test('il documento login contiene il form e resta disponibile senza cookie e API', async ({
  page,
}) => {
  let calls = 0
  setSsrSession(page, () => {
    calls++
    return { user: null, offline: true }
  })
  await page.context().clearCookies()
  const response = await page.request.get('/login')
  expect(response.status()).toBe(200)
  expect(response.headers()['cache-control']).toContain('no-store')
  const html = await response.text()
  expect(html).toContain('<form')
  expect(html).toContain('type="email"')
  expect(html).toContain('type="password"')
  expect(html).not.toContain('Caricamento in corso')
  expect(calls).toBe(0)
})

test('il server reindirizza una sessione valida prima di mostrare la login', async ({
  page,
}) => {
  setSsrSession(page, () => ({ user }))
  const response = await page.request.get('/login', { maxRedirects: 0 })
  expect(response.status()).toBe(307)
  expect(response.headers().location).toMatch(/^\/leghe/)
  expect(response.headers()['cache-control']).toContain('no-store')
  expect(await response.text()).not.toContain('<form')
})

test('il guard server protegge anche l’accesso diretto alla sala d’asta', async ({
  page,
}) => {
  const response = await page.request.get('/leghe/private/asta', {
    maxRedirects: 0,
  })
  expect(response.status()).toBe(307)
  expect(response.headers().location).toBe('/login')
})

test('richieste contemporanee non condividono l’identità o la cache privata', async ({
  page,
  request,
}) => {
  setSsrSession(page, () => ({ user }))
  const [privateResponse, publicResponse] = await Promise.all([
    page.request.get('/leghe?page=1'),
    request.get('/login'),
  ])
  expect(await privateResponse.text()).toContain(user.email)
  expect(privateResponse.headers()['cache-control']).toContain('no-store')
  const publicHtml = await publicResponse.text()
  expect(publicHtml).toContain('<form')
  expect(publicHtml).not.toContain(user.email)
  expect(publicHtml).not.toContain('Fantastiche.Auth')
})

test('la login si attiva senza errori di hydration o nuova verifica bloccante', async ({
  page,
}) => {
  const errors: string[] = []
  page.on('pageerror', (error) => errors.push(error.message))
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text())
  })
  let checks = 0
  await page.route('http://localhost:6060/api/Auth/Me', (route) => {
    checks++
    return route.fulfill({
      status: 401,
      json: { isSuccess: false, data: null, errors: [] },
    })
  })
  await page.goto('/login')
  await page.getByLabel('Password', { exact: true }).fill('example')
  await page.getByRole('button', { name: 'Mostra password' }).click()
  await expect(page.getByLabel('Password', { exact: true })).toHaveAttribute(
    'type',
    'text',
  )
  expect(checks).toBe(0)
  expect(errors).toEqual([])
})

test.describe('senza JavaScript', () => {
  test.use({ javaScriptEnabled: false })
  test('il form resta visibile dopo il refresh', async ({ page }) => {
    await page.goto('/login')
    await page.reload()
    await expect(
      page.getByRole('heading', { name: 'Bentornato in campo.' }),
    ).toBeVisible()
    await expect(page.getByLabel('Email', { exact: true })).toBeVisible()
    await expect(
      page.getByRole('button', { name: 'Accedi', exact: true }),
    ).toBeVisible()
    await expect(page.getByLabel('Email', { exact: true })).toBeDisabled()
    await expect(page.locator('form')).toHaveAttribute('method', 'post')
  })

  test('i form amministrativi attendono React prima di accettare dati', async ({
    page,
  }) => {
    setSsrSession(page, () => ({ user: { ...user, isSuperAdmin: true } }))
    await page.goto('/leghe/nuova')
    await expect(
      page.getByLabel('Email dell’organizzatore', { exact: true }),
    ).toBeDisabled()
    await expect(
      page.getByRole('button', { name: 'Crea lega', exact: true }),
    ).toBeDisabled()
    await page.goto('/catalogo')
    await expect(page.getByLabel('Stagione del listone')).toBeDisabled()
    await expect(page.getByLabel('Filtra per stagione')).toBeDisabled()
    await expect(
      page.getByRole('button', { name: 'Importa in bozza' }),
    ).toBeDisabled()
  })
})
