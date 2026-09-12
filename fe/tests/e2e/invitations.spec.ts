import { setSsrSession } from '../ssr-fixture'
import { expect, test, type Page } from '../ssr-fixture'

const token = 'A'.repeat(64)
const user = {
  id: 'user-invite',
  email: 'invite@example.test',
  displayName: 'Giulia',
  isSuperAdmin: false,
}
const league = {
  id: 'league-invite',
  name: 'Amici del sabato',
  leagueSeasonId: 'season-invite',
  seasonName: '2026/27',
  budget: 500,
  goalkeepers: 3,
  defenders: 8,
  midfielders: 8,
  forwards: 6,
}

async function setup(
  page: Page,
  options: {
    existing?: boolean
    authenticated?: boolean
    canManage?: boolean
    unavailable?: string
    wrongAccount?: boolean
    requiresTeam?: boolean
  } = {},
) {
  const state = {
    authenticated: options.authenticated ?? false,
    accepted: false,
    invites: [] as {
      id: string
      displayName: string
      email: string
      status: string
      kind: string
      expiresAt: string
    }[],
    accepts: [] as Record<string, unknown>[],
    sent: 0,
  }
  setSsrSession(page, () => ({ user: state.authenticated ? user : null }))
  await page.route('http://localhost:6060/api/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    expect(url.searchParams.has('token')).toBe(false)
    const respond = (
      data: unknown,
      status = 200,
      code = 'test.error',
      message = 'Operazione non consentita.',
    ) =>
      route.fulfill({
        status,
        json: {
          isSuccess: status < 400,
          data,
          errors: status < 400 ? [] : [{ code, message }],
        },
      })
    if (url.pathname.endsWith('/Antiforgery')) return respond({ token: 'csrf' })
    if (request.method() === 'POST')
      expect(request.headers()['x-xsrf-token']).toBe('csrf')
    if (url.pathname.endsWith('/Me'))
      return respond(
        state.authenticated ? user : null,
        state.authenticated ? 200 : 401,
      )
    if (url.pathname.endsWith('/Login')) {
      state.authenticated = true
      return respond(user)
    }
    if (url.pathname.endsWith('/Logout')) {
      state.authenticated = false
      return respond({ signedOut: true })
    }
    if (url.pathname.endsWith('/Invitations/Preview')) {
      expect(request.headers()['x-invitation-token']).toBe(token)
      if (options.unavailable)
        return respond(
          null,
          410,
          `invitation.${options.unavailable}`,
          options.unavailable === 'expired'
            ? 'Invito scaduto.'
            : 'Invito revocato.',
        )
      return respond({
        leagueName: league.name,
        leagueLogoUrl: null,
        invitedBy: 'Marco Bianchi',
        recipientEmailHint: 'i•••e@example.test',
        expiresAt: '2026-12-01T12:00:00Z',
        requiresLogin: options.existing ?? false,
        requiresTeam: options.requiresTeam ?? true,
      })
    }
    if (url.pathname.endsWith('/Invitations/Accept')) {
      const body = request.postDataJSON() as Record<string, unknown>
      state.accepts.push(body)
      expect(body.token).toBe(token)
      if (options.wrongAccount)
        return respond(
          null,
          403,
          'auth.forbidden',
          'Operazione non consentita.',
        )
      state.accepted = true
      return respond({
        leagueId: league.id,
        leagueSeasonId: league.leagueSeasonId,
        teamId: body.teamName ? 'team-invite' : null,
        email: user.email,
        teamName: body.teamName ?? null,
      })
    }
    if (url.pathname.endsWith('/Participants'))
      return respond({
        leagueId: league.id,
        leagueSeasonId: league.leagueSeasonId,
        canManage: options.canManage ?? false,
        participants: [
          {
            userId: user.id,
            displayName: user.displayName,
            teamName: 'Le Fenici',
            isOrganizer: true,
          },
        ],
        invitations: {
          items: state.invites,
          totalCount: state.invites.length,
          page: 1,
          pageSize: 20,
        },
      })
    if (url.pathname.endsWith('/Catalog')) return respond(null)
    if (url.pathname.endsWith('/AuctionRoom'))
      return respond({ canManage: false })
    if (url.pathname.endsWith('/Invitations')) {
      const body = request.postDataJSON() as {
        displayName: string
        email: string
      }
      state.sent++
      state.invites.unshift({
        ...body,
        id: `invite-${state.sent}`,
        status: 'Pending',
        kind: 'Participant',
        expiresAt: '2026-12-01T12:00:00Z',
      })
      return respond(
        {
          id: `invite-${state.sent}`,
          leagueId: league.id,
          expiresAt: '2026-12-01T12:00:00Z',
        },
        201,
      )
    }
    if (url.pathname.endsWith('/Resend')) {
      state.invites[0]!.status = 'Revoked'
      state.invites.unshift({
        ...state.invites[0]!,
        id: 'resent',
        status: 'Pending',
      })
      return respond({
        id: 'resent',
        leagueId: league.id,
        expiresAt: '2026-12-01T12:00:00Z',
      })
    }
    if (url.pathname.endsWith('/Revoke')) {
      state.invites[0]!.status = 'Revoked'
      return respond(true)
    }
    if (url.pathname.endsWith(`/Leagues/${league.id}`)) return respond(league)
    if (url.pathname.endsWith('/Leagues'))
      return respond({ items: [league], totalCount: 1, page: 1, pageSize: 20 })
    return respond(null, 404)
  })
  return state
}

test('nuovo account: attivazione, nome squadra e login senza token in URL o storage', async ({
  page,
}) => {
  const state = await setup(page)
  await page.goto(`/invito#token=${token}`)
  await expect(
    page.getByRole('heading', { name: /Amici del sabato/ }),
  ).toBeVisible()
  await expect(page).toHaveURL(/\/invito$/)
  await expect(page.getByText('Marco Bianchi')).toBeVisible()
  await page.getByLabel('Nome squadra', { exact: true }).fill('Le Fenici')
  await expect(page.getByText(/apparirai come/)).toContainText('Le Fenici')
  await page.getByLabel('Password', { exact: true }).fill('Invited-User-123!')
  await page.getByRole('button', { name: 'Attiva account e partecipa' }).click()
  await expect(
    page.getByRole('heading', { name: `Sei in ${league.name}.` }),
  ).toBeVisible()
  expect(state.accepts).toEqual([
    { token, password: 'Invited-User-123!', teamName: 'Le Fenici' },
  ])
  await expect(page.getByLabel('Email', { exact: true })).toHaveValue(
    user.email,
  )
  await page.getByLabel('Password', { exact: true }).fill('Invited-User-123!')
  await page.getByRole('button', { name: 'Accedi', exact: true }).click()
  await expect(page).toHaveURL(new RegExp(`/leghe/${league.id}/?$`))
  expect(
    await page.evaluate(() =>
      JSON.stringify({ ...localStorage, ...sessionStorage }),
    ),
  ).not.toContain(token)
})

test('account esistente: login in pagina e accettazione senza password nel payload', async ({
  page,
}) => {
  const state = await setup(page, { existing: true })
  await page.goto(`/invito?token=${token}`)
  await expect(page).toHaveURL(/\/invito$/)
  await expect(page.getByText('i•••e@example.test')).toBeVisible()
  await expect(page.locator('li[aria-current="step"]')).toHaveText(/Accedi/)
  await page.getByLabel('Email', { exact: true }).fill(user.email)
  await page.getByLabel('Password', { exact: true }).fill('Invited-User-123!')
  await page.getByRole('button', { name: 'Accedi', exact: true }).click()
  await expect(page.getByText(/Stai accettando come/)).toContainText(user.email)
  await expect(page.locator('li[aria-current="step"]')).toHaveText(
    /Conferma squadra/,
  )
  await page.getByLabel('Nome squadra', { exact: true }).fill('Le Fenici')
  await page.getByRole('button', { name: `Entra in ${league.name}` }).click()
  await expect(page).toHaveURL(new RegExp(`/leghe/${league.id}/?$`))
  expect(state.accepts[0]).toEqual({ token, teamName: 'Le Fenici' })
})

test('account diverso può uscire senza perdere invito; errore server resta visibile', async ({
  page,
}) => {
  await setup(page, { existing: true, authenticated: true, wrongAccount: true })
  await page.goto(`/invito#token=${token}`)
  await page.getByLabel('Nome squadra', { exact: true }).fill('Le Fenici')
  await page.getByRole('button', { name: `Entra in ${league.name}` }).click()
  await expect(page.getByRole('alert')).toContainText(/destinatario/)
  await page.getByRole('button', { name: 'Cambia account' }).last().click()
  await expect(page.getByLabel('Email', { exact: true })).toBeVisible()
  await expect(page).toHaveURL(/\/invito$/)
})

for (const unavailable of ['expired', 'revoked'])
  test(`invito ${unavailable}: errore esplicito senza form attivazione`, async ({
    page,
  }) => {
    await setup(page, { unavailable })
    await page.goto(`/invito#token=${token}`)
    await expect(page.getByRole('alert')).toContainText(
      unavailable === 'expired' ? /scaduto/ : /revocato/,
    )
    await expect(page.getByRole('link', { name: /Accedi/ })).toHaveAttribute(
      'href',
      /\/login$/,
    )
    await expect(page.getByLabel('Password', { exact: true })).toHaveCount(0)
  })

test('organizzatore invita, reinvia e revoca dal dettaglio lega', async ({
  page,
}) => {
  const state = await setup(page, { authenticated: true, canManage: true })
  await page.goto(`/leghe/${league.id}`)
  await page.getByLabel('Nome partecipante').fill('Luca')
  await page.getByLabel('Email partecipante').fill('luca@example.test')
  await page.getByRole('button', { name: 'Invia invito', exact: true }).click()
  await expect(page.getByText('luca@example.test')).toBeVisible()
  expect(state.sent).toBe(1)
  await page.getByRole('button', { name: 'Reinvia', exact: true }).click()
  await expect(page.getByText('Revocato', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Revoca', exact: true }).click()
  await page.getByRole('button', { name: 'Conferma revoca' }).click()
  await expect(
    page.getByRole('button', { name: 'Reinvia', exact: true }),
  ).toHaveCount(0)
})

test('partecipante non riceve i controlli di gestione', async ({ page }) => {
  await setup(page, { authenticated: true })
  await page.goto(`/leghe/${league.id}`)
  await expect(page.getByRole('heading', { name: league.name })).toBeVisible()
  await expect(
    page.getByRole('button', { name: 'Invia invito', exact: true }),
  ).toHaveCount(0)
})

test('invito organizzatore attiva account senza creare squadra', async ({
  page,
}) => {
  const state = await setup(page, { requiresTeam: false })
  await page.goto(`/invito#token=${token}`)
  await expect(page.getByLabel('Nome squadra', { exact: true })).toHaveCount(0)
  await expect(page.getByText('Organizzatore', { exact: true })).toBeVisible()
  await page.getByLabel('Password', { exact: true }).fill('Invited-User-123!')
  await page.getByRole('button', { name: 'Attiva account e organizza' }).click()
  await expect(
    page.getByRole('heading', { name: `Sei in ${league.name}.` }),
  ).toBeVisible()
  expect(state.accepts[0]).toEqual({ token, password: 'Invited-User-123!' })
})

test('una password debole blocca la richiesta e la checklist lo mostra', async ({
  page,
}) => {
  const state = await setup(page)
  await page.goto(`/invito#token=${token}`)
  await page.getByLabel('Nome squadra', { exact: true }).fill('Le Fenici')
  await page.getByLabel('Password', { exact: true }).fill('tuttominuscolo123')
  const rules = page.getByRole('list', { name: 'Requisiti della password' })
  await expect(rules.getByText('Un numero')).toBeVisible()
  await expect(
    rules.locator('li[data-satisfied="false"]').getByText('Una maiuscola'),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Attiva account e partecipa' }).click()
  await expect(page.getByRole('alert')).toContainText('Aggiungi una maiuscola.')
  expect(state.accepts).toHaveLength(0)
})

for (const width of [320, 390, 1280])
  test(`inviti e gestione leggibili a ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 })
    await setup(page)
    await page.goto(`/invito#token=${token}`)
    await expect(page.getByLabel('Nome squadra', { exact: true })).toBeVisible()
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    await page.screenshot({
      path: test.info().outputPath(`invitation-${width}.png`),
      fullPage: true,
    })
    await setup(page, { authenticated: true, canManage: true })
    await page.goto(`/leghe/${league.id}`)
    await expect(page.getByLabel('Nome partecipante')).toBeVisible()
    // Il badge del ruolo vive solo nel layout largo: verifica che il CSS del
    // pannello partecipanti non regredisca.
    await expect(page.locator('.participant-badge')).toBeVisible({
      visible: width >= 700,
    })
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    await page.screenshot({
      path: test.info().outputPath(`participants-${width}.png`),
      fullPage: true,
    })
  })
