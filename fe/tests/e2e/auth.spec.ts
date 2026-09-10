import { test, expect, type Page } from '@playwright/test'

const user = {
  id: 'user-1',
  email: 'member@example.test',
  displayName: 'Giulia Riva',
  isSuperAdmin: false,
}
const league = {
  id: 'league-1',
  name: 'Lega del mercoledì',
  leagueSeasonId: 'season-1',
  seasonName: '2026/27',
  budget: 500,
  goalkeepers: 3,
  defenders: 8,
  midfielders: 8,
  forwards: 6,
}
async function setupApi(page: Page, initialSession = false) {
  const state = {
    authenticated: initialSession,
    offline: false,
    expired: false,
    requests: [] as string[],
    loginCount: 0,
  }
  await page.route('http://localhost:6060/api/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    state.requests.push(url.pathname)
    const respond = (data: unknown, status = 200) =>
      route.fulfill({
        status,
        json: {
          isSuccess: status === 200,
          data,
          errors:
            status === 200
              ? []
              : [{ code: 'auth.required', message: 'Accesso richiesto.' }],
        },
      })
    if (state.offline) return route.abort('failed')
    if (url.pathname.endsWith('/Antiforgery'))
      return respond({ token: 'csrf-test' })
    if (url.pathname.endsWith('/Me'))
      return respond(
        state.authenticated ? user : null,
        state.authenticated ? 200 : 401,
      )
    if (url.pathname.endsWith('/Login')) {
      expect(request.headers()['x-xsrf-token']).toBe('csrf-test')
      state.loginCount++
      const body: unknown = request.postDataJSON()
      expect(body).toEqual({ email: user.email, password: 'test-password' })
      state.authenticated = true
      return respond(user)
    }
    if (url.pathname.endsWith('/Logout')) {
      state.authenticated = false
      return respond({ signedOut: true })
    }
    if (state.expired) {
      state.authenticated = false
      return respond(null, 401)
    }
    if (!state.authenticated) return respond(null, 401)
    if (url.pathname.endsWith('/Leagues/league-1')) return respond(league)
    if (url.pathname.endsWith('/Leagues'))
      return respond({ items: [league], totalCount: 1, page: 1, pageSize: 20 })
    return route.fulfill({ status: 404 })
  })
  return state
}

test('accesso, elenco, dettaglio e logout cancellano i contenuti privati', async ({
  page,
}) => {
  const state = await setupApi(page)
  await page.goto('/leghe')
  await expect(page).toHaveURL(/\/login$/)
  await page.getByLabel('Email', { exact: true }).fill(user.email)
  await page.getByLabel('Password', { exact: true }).fill('test-password')
  await page.getByRole('button', { name: 'Accedi', exact: true }).click()
  await expect(
    page.getByRole('heading', { name: 'Le mie leghe' }),
  ).toBeVisible()
  expect(state.loginCount).toBe(1)
  await page.getByRole('link', { name: /Lega del mercoledì/ }).click()
  await expect(
    page.getByRole('heading', { name: 'Configurazione della rosa' }),
  ).toBeVisible()
  await expect(page.getByText('500')).toBeVisible()
  await page.getByRole('button', { name: 'Apri menu profilo' }).click()
  await page.getByRole('menuitem', { name: 'Esci', exact: true }).click()
  await expect(page).toHaveURL(/\/login$/)
  await page.goBack()
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByText(league.name)).toHaveCount(0)
})

test('sessione scaduta durante la consultazione torna al login', async ({
  page,
}) => {
  const state = await setupApi(page, true)
  await page.goto('/leghe')
  await expect(
    page.getByRole('heading', { name: 'Le mie leghe' }),
  ).toBeVisible()
  state.expired = true
  await page.getByRole('link', { name: /Lega del mercoledì/ }).click()
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByText(league.name)).toHaveCount(0)
})

test('errore di rete mostra Riprova senza fingere una sessione scaduta', async ({
  page,
}) => {
  const state = await setupApi(page, true)
  state.offline = true
  await page.goto('/leghe')
  await expect(page.getByRole('alert')).toContainText(/connessione/)
  await expect(page).toHaveURL(/\/leghe/)
  state.offline = false
  await page.getByRole('button', { name: 'Riprova' }).click()
  await expect(
    page.getByRole('heading', { name: 'Le mie leghe' }),
  ).toBeVisible()
})

test('elenco vuoto è esplicito e non propone la creazione al partecipante', async ({
  page,
}) => {
  await setupApi(page, true)
  await page.route('http://localhost:6060/api/Leagues?**', (route) =>
    route.fulfill({
      json: {
        isSuccess: true,
        data: { items: [], totalCount: 0, page: 1, pageSize: 20 },
        errors: [],
      },
    }),
  )
  await page.goto('/leghe')
  await expect(
    page.getByRole('heading', {
      name: 'La tua stagione deve ancora cominciare',
    }),
  ).toBeVisible()
  await expect(page.getByRole('button', { name: /Crea/ })).toHaveCount(0)
})

test('paginazione richiesta all’API e recupero di una pagina vuota', async ({
  page,
}) => {
  await setupApi(page, true)
  await page.route('http://localhost:6060/api/Leagues?**', (route) => {
    const currentPage = Number(
      new URL(route.request().url()).searchParams.get('page'),
    )
    return route.fulfill({
      json: {
        isSuccess: true,
        data: {
          items: currentPage === 1 ? [league] : [],
          totalCount: currentPage === 1 ? 21 : 0,
          page: currentPage,
          pageSize: 20,
        },
        errors: [],
      },
    })
  })
  await page.goto('/leghe')
  await page.getByRole('link', { name: 'Successiva' }).click()
  await expect(page).toHaveURL(/page=2/)
  await expect(
    page.getByRole('heading', { name: 'Nessuna lega in questa pagina' }),
  ).toBeVisible()
  await page
    .getByRole('link', { name: 'Torna alle leghe', exact: true })
    .click()
  await expect(page.getByRole('heading', { name: league.name })).toBeVisible()
})

for (const viewport of [
  { width: 1440, height: 1000 },
  { width: 390, height: 844 },
  { width: 320, height: 568 },
]) {
  test(`login centrato e navigabile da tastiera a ${viewport.width}px`, async ({
    page,
  }) => {
    await setupApi(page)
    await page.setViewportSize(viewport)
    await page.goto('/login')
    await expect(
      page.getByRole('heading', { name: 'Bentornato in campo.' }),
    ).toBeVisible()
    const panel = await page.locator('.login-panel').boundingBox()
    expect(panel).not.toBeNull()
    expect(
      Math.abs((panel?.x ?? 0) + (panel?.width ?? 0) / 2 - viewport.width / 2),
    ).toBeLessThan(2)
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true)
    await page.keyboard.press('Tab')
    await expect(page.getByLabel('Email', { exact: true })).toBeFocused()
    await page.keyboard.press('Tab')
    await expect(page.getByLabel('Password', { exact: true })).toBeFocused()
    await page.keyboard.press('Tab')
    await expect(
      page.getByRole('button', { name: 'Mostra password' }),
    ).toBeFocused()
    await page.screenshot({
      path: test.info().outputPath(`login-${viewport.width}.png`),
      fullPage: true,
    })
  })
}

for (const width of [320, 1440]) {
  test(`profilo con nome ed email e dropdown accessibile a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 })
    await setupApi(page, true)
    await page.goto('/leghe')
    const trigger = page.getByRole('button', { name: 'Apri menu profilo' })
    const menu = page.getByRole('menu', { name: 'Menu profilo' })
    await expect(trigger).toBeVisible()
    await expect(menu).toHaveCount(0)
    await expect(page.getByRole('menuitem', { name: 'Esci' })).toHaveCount(0)
    if (width > 600) {
      await expect(
        trigger.getByText('Giulia Riva', { exact: true }),
      ).toBeVisible()
      await expect(trigger.getByText(user.email, { exact: true })).toBeVisible()
    }
    await trigger.focus()
    await page.keyboard.press('ArrowDown')
    await expect(menu).toBeVisible()
    await expect(menu.getByText('Giulia Riva', { exact: true })).toBeVisible()
    await expect(menu.getByText(user.email, { exact: true })).toBeVisible()
    await expect(menu.getByRole('menuitem', { name: 'Esci' })).toBeFocused()
    const box = (await menu.boundingBox())!
    expect(box.x).toBeGreaterThanOrEqual(0)
    expect(box.x + box.width).toBeLessThanOrEqual(width)
    await page.screenshot({
      path: test.info().outputPath(`profile-${width}.png`),
    })
    await page.keyboard.press('Escape')
    await expect(menu).toHaveCount(0)
    await expect(trigger).toBeFocused()
    await trigger.click()
    await page.mouse.click(10, 400)
    await expect(menu).toHaveCount(0)
  })
}

test('un logout fallito resta nel menu profilo e consente di riprovare', async ({
  page,
}) => {
  await setupApi(page, true)
  let failed = true
  await page.route('http://localhost:6060/api/Auth/Logout', async (route) => {
    if (!failed) return route.fallback()
    return route.fulfill({
      status: 500,
      json: {
        isSuccess: false,
        data: null,
        errors: [{ code: 'server.error', message: 'Uscita non riuscita.' }],
      },
    })
  })
  await page.goto('/leghe')
  await page.getByRole('button', { name: 'Apri menu profilo' }).click()
  await page.getByRole('menuitem', { name: 'Esci' }).click()
  await expect(
    page.getByRole('menu', { name: 'Menu profilo' }).getByRole('alert'),
  ).toBeVisible()
  await expect(page).toHaveURL(/leghe/)
  failed = false
  await page.getByRole('menuitem', { name: 'Esci' }).click()
  await expect(page).toHaveURL(/login/)
})
