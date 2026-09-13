import { setSsrSession } from '../ssr-fixture'
import { expect, test, type Page } from '../ssr-fixture'

const admin = {
  id: 'admin-1',
  email: 'admin@example.test',
  displayName: 'Admin',
  isSuperAdmin: true,
}
const values = {
  name: 'Lega degli amici',
  seasonName: '2026/27',
  organizerName: 'Giulia Rossi',
  organizerEmail: 'giulia@example.test',
  budget: 500,
  goalkeepers: 3,
  defenders: 8,
  midfielders: 8,
  forwards: 6,
}
const league = { ...values, id: 'league-new', leagueSeasonId: 'season-new' }

async function setupApi(
  page: Page,
  role: 'admin' | 'member' | 'anonymous' = 'admin',
) {
  const state = {
    posts: [] as unknown[],
    failure: 0,
    created: false,
    hold: undefined as Promise<void> | undefined,
  }
  setSsrSession(page, () => ({
    user:
      role === 'anonymous'
        ? null
        : { ...admin, isSuperAdmin: role === 'admin' },
  }))
  await page.route('http://localhost:6060/api/**', async (route) => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    const respond = (data: unknown, status = 200) =>
      route.fulfill({
        status,
        json: {
          isSuccess: status === 200,
          data,
          errors:
            status === 200
              ? []
              : [
                  {
                    code: status === 401 ? 'auth.required' : 'league.error',
                    message:
                      status === 401
                        ? 'Accesso richiesto.'
                        : 'Impossibile creare la lega. Riprova.',
                  },
                ],
        },
      })
    if (path.endsWith('/Me'))
      return respond(
        role === 'anonymous'
          ? null
          : { ...admin, isSuperAdmin: role === 'admin' },
        role === 'anonymous' ? 401 : 200,
      )
    if (path.endsWith('/Antiforgery')) return respond({ token: 'csrf-create' })
    if (path === '/api/Leagues' && request.method() === 'POST') {
      expect(request.headers()['x-xsrf-token']).toBe('csrf-create')
      state.posts.push(request.postDataJSON())
      await state.hold
      if (state.failure) return respond(null, state.failure)
      state.created = true
      return respond(league)
    }
    if (path === '/api/Leagues')
      return respond({
        items: state.created ? [league] : [],
        totalCount: state.created ? 1 : 0,
        page: 1,
        pageSize: 20,
      })
    if (path === '/api/Leagues/league-new') return respond(league)
    return route.fulfill({ status: 404 })
  })
  return state
}

async function fillForm(page: Page) {
  await page
    .getByLabel('Nome della lega', { exact: true })
    .fill(` ${values.name} `)
  await page.getByLabel('Stagione', { exact: true }).fill(values.seasonName)
  await page
    .getByLabel('Nome dell’organizzatore', { exact: true })
    .fill(values.organizerName)
  await page
    .getByLabel('Email dell’organizzatore', { exact: true })
    .fill(values.organizerEmail)
}

test('SuperAdmin crea la lega e ritrova il nuovo elemento nell’elenco', async ({
  page,
}) => {
  const state = await setupApi(page)
  await page.goto('/leghe')
  await page.getByRole('link', { name: 'Crea lega', exact: true }).click()
  await expect(page).toHaveURL(/\/leghe\/nuova$/)
  await fillForm(page)
  await expect(page.getByText('Totale rosa')).toContainText('25')
  await page.getByRole('button', { name: 'Crea lega', exact: true }).click()
  await expect(page).toHaveURL(/\/leghe\/league-new$/)
  expect(state.posts).toEqual([values])
  await expect(page.getByRole('heading', { name: values.name })).toBeVisible()
  await page.getByRole('link', { name: 'Torna alle leghe' }).click()
  await expect(page.getByRole('heading', { name: values.name })).toBeVisible()
})

test('campi obbligatori e budget insufficiente impediscono la richiesta', async ({
  page,
}) => {
  const state = await setupApi(page)
  await page.goto('/leghe/nuova')
  await page.getByRole('button', { name: 'Crea lega', exact: true }).click()
  await expect(
    page.getByLabel('Nome della lega', { exact: true }),
  ).toBeFocused()
  expect(state.posts).toHaveLength(0)
  await fillForm(page)
  await page.getByLabel('Budget per squadra').fill('24')
  await page.getByRole('button', { name: 'Crea lega', exact: true }).click()
  await expect(
    page.getByText('Prevedi almeno un credito per ogni giocatore in rosa.'),
  ).toBeVisible()
  expect(state.posts).toHaveLength(0)
})

test('blocca gli invii multipli mentre la creazione è in corso', async ({
  page,
}) => {
  const state = await setupApi(page)
  let release: () => void = () => {}
  state.hold = new Promise<void>((resolve) => {
    release = resolve
  })
  await page.goto('/leghe/nuova')
  await fillForm(page)
  await page.getByRole('button', { name: 'Crea lega', exact: true }).dblclick()
  await expect(
    page.getByRole('button', { name: 'Creazione in corso…' }),
  ).toBeDisabled()
  await expect(
    page.getByLabel('Nome della lega', { exact: true }),
  ).toBeDisabled()
  await page.locator('.create-league-form').evaluate((element) => {
    const formElement = element as HTMLFormElement
    formElement.requestSubmit()
    formElement.requestSubmit()
  })
  await expect.poll(() => state.posts.length).toBe(1)
  release()
  await expect(page).toHaveURL(/\/leghe\/league-new$/)
  expect(state.posts).toHaveLength(1)
})

test('errore API conserva i dati e consente un nuovo tentativo esplicito', async ({
  page,
}) => {
  const state = await setupApi(page)
  state.failure = 500
  await page.goto('/leghe/nuova')
  await fillForm(page)
  await page.getByRole('button', { name: 'Crea lega', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText(
    'Il servizio non è disponibile al momento.',
  )
  await expect(page.getByLabel('Nome della lega', { exact: true })).toHaveValue(
    values.name,
  )
  expect(state.posts).toHaveLength(1)
  state.failure = 0
  await page.getByRole('button', { name: 'Crea lega', exact: true }).click()
  await expect(page).toHaveURL(/\/leghe\/league-new$/)
  expect(state.posts).toHaveLength(2)
})

for (const role of ['member', 'anonymous'] as const) {
  test(`accesso diretto alla creazione negato: ${role}`, async ({ page }) => {
    const state = await setupApi(page, role)
    await page.goto('/leghe/nuova')
    await expect(page).toHaveURL(
      role === 'member' ? /\/leghe\/?(\?page=1)?$/ : /\/login$/,
    )
    await expect(
      page.getByRole('link', { name: 'Crea lega', exact: true }),
    ).toHaveCount(0)
    await expect(
      page.getByRole('button', { name: 'Crea lega', exact: true }),
    ).toHaveCount(0)
    expect(state.posts).toHaveLength(0)
  })
}

test('sessione scaduta durante il salvataggio torna al login', async ({
  page,
}) => {
  const state = await setupApi(page)
  state.failure = 401
  await page.goto('/leghe/nuova')
  await fillForm(page)
  await page.route('http://localhost:6060/api/Auth/Me', (route) =>
    route.fulfill({
      status: 401,
      json: {
        isSuccess: false,
        data: null,
        errors: [{ code: 'auth.required', message: 'Accesso richiesto.' }],
      },
    }),
  )
  await page.getByRole('button', { name: 'Crea lega', exact: true }).click()
  await expect(page).toHaveURL(/\/login$/)
  expect(state.posts).toHaveLength(1)
})

for (const width of [1440, 390, 320]) {
  test(`form di creazione leggibile a ${width}px`, async ({ page }) => {
    await setupApi(page)
    await page.setViewportSize({ width, height: 1000 })
    await page.goto('/leghe/nuova')
    await expect(
      page.getByRole('heading', { name: 'Crea una lega' }),
    ).toBeVisible()
    expect(
      await page.evaluate(async () => {
        await document.fonts.load('300 16px "Font Awesome 7 Pro"')
        return document.fonts.check('300 16px "Font Awesome 7 Pro"')
      }),
    ).toBe(true)
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= window.innerWidth,
      ),
    ).toBe(true)
    await page.screenshot({
      path: test.info().outputPath(`create-league-${width}.png`),
      fullPage: true,
    })
  })
}

const logoPng =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl1sAAAAASUVORK5CYII='

test('carica il logo con anteprima e lo invia insieme alla lega', async ({
  page,
}) => {
  const state = await setupApi(page)
  await page.goto('/leghe/nuova')
  await fillForm(page)
  await page.getByLabel('File logo della lega').setInputFiles({
    name: 'stemma.png',
    mimeType: 'image/png',
    buffer: Buffer.from(logoPng, 'base64'),
  })
  await expect(
    page.getByRole('img', { name: 'Anteprima del logo' }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Crea lega', exact: true }).click()
  await expect(page).toHaveURL(/\/leghe\/league-new$/)
  expect(state.posts).toEqual([{ ...values, logo: logoPng }])
})

test('rifiuta file non immagine e permette di rimuoverli', async ({ page }) => {
  const state = await setupApi(page)
  await page.goto('/leghe/nuova')
  await fillForm(page)
  await page.getByLabel('File logo della lega').setInputFiles({
    name: 'logo.svg',
    mimeType: 'image/svg+xml',
    buffer: Buffer.from('<svg/>'),
  })
  await expect(page.getByRole('alert')).toContainText('PNG, JPEG o WebP')
  await expect(
    page.getByRole('button', { name: 'Crea lega', exact: true }),
  ).toBeDisabled()
  expect(state.posts).toHaveLength(0)
  await page.getByRole('button', { name: 'Rimuovi logo' }).click()
  await expect(
    page.getByRole('button', { name: 'Crea lega', exact: true }),
  ).toBeEnabled()
})

test('trascinamento e sostituzione del logo funzionano anche su mobile', async ({
  page,
}) => {
  await setupApi(page)
  await page.setViewportSize({ width: 390, height: 844 })
  await page.goto('/leghe/nuova')
  await expect(
    page.getByRole('button', { name: 'Scegli logo della lega' }),
  ).toBeEnabled()
  const transfer = await page.evaluateHandle((base64) => {
    const data = new DataTransfer()
    data.items.add(
      new File(
        [Uint8Array.from(atob(base64), (char) => char.charCodeAt(0))],
        'logo-trascinato.png',
        { type: 'image/png' },
      ),
    )
    return data
  }, logoPng)
  await page
    .locator('.league-logo-dropzone')
    .dispatchEvent('dragover', { dataTransfer: transfer })
  await expect(page.locator('.league-logo-dropzone')).toHaveAttribute(
    'data-dragging',
    'true',
  )
  await page
    .locator('.league-logo-dropzone')
    .dispatchEvent('drop', { dataTransfer: transfer })
  await expect(page.getByText('logo-trascinato.png')).toBeVisible()
  await expect(
    page.getByRole('img', { name: 'Anteprima del logo' }),
  ).toBeVisible()
  await page.screenshot({
    path: test.info().outputPath('logo-mobile.png'),
    fullPage: true,
  })
  await page.getByRole('button', { name: 'Rimuovi logo' }).click()
  await expect(
    page.getByRole('img', { name: 'Anteprima del logo' }),
  ).toHaveCount(0)
  await page.setViewportSize({ width: 1440, height: 1100 })
  await page.screenshot({
    path: test.info().outputPath('logo-desktop.png'),
    fullPage: true,
  })
  const button = page.getByRole('button', { name: 'Scegli logo della lega' })
  await button.focus()
  const chooser = page.waitForEvent('filechooser')
  await page.keyboard.press('Enter')
  await (
    await chooser
  ).setFiles({
    name: 'altro.png',
    mimeType: 'image/png',
    buffer: Buffer.from(logoPng, 'base64'),
  })
  await expect(page.getByText('altro.png')).toBeVisible()
  await transfer.dispose()
})

test('un logo oltre 2 MB blocca il salvataggio senza perdere gli altri campi', async ({
  page,
}) => {
  const state = await setupApi(page)
  await page.goto('/leghe/nuova')
  await fillForm(page)
  await page.getByLabel('File logo della lega').setInputFiles({
    name: 'grande.png',
    mimeType: 'image/png',
    buffer: Buffer.alloc(2 * 1024 * 1024 + 1),
  })
  await expect(page.getByRole('alert')).toContainText('al massimo 2 MB')
  await expect(
    page.getByRole('button', { name: 'Crea lega', exact: true }),
  ).toBeDisabled()
  await expect(page.getByLabel('Nome della lega', { exact: true })).toHaveValue(
    values.name,
  )
  expect(state.posts).toHaveLength(0)
})
