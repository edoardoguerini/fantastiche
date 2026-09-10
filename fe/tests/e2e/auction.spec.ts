import { test, expect, type Page, type WebSocketRoute } from '@playwright/test'

const sid = '00000000-0000-4000-8000-000000000001'
const aid = '00000000-0000-4000-8000-000000000002'
const playerId = '00000000-0000-4000-8000-000000000003'
const league = {
  id: 'league-1',
  name: 'Lega di prova',
  leagueSeasonId: 'season-1',
  seasonName: '2026/27',
  budget: 500,
  goalkeepers: 3,
  defenders: 8,
  midfielders: 8,
  forwards: 6,
}
const user = {
  id: 'user-1',
  email: 'demo@example.test',
  displayName: 'Demo',
  isSuperAdmin: false,
}
const url = '/leghe/league-1/asta'
async function setupRoom(
  page: Page,
  options: { organizer?: boolean; waiting?: boolean; setup?: boolean } = {},
) {
  let roomSessionId: string | null = options.setup ? null : sid
  const teams = [
    'Atletico Spritz',
    'Real Sbronzi',
    'Dinamo Divano',
    'Sporting Aperitivo',
  ].map((name, i) => ({
    id: `team-${i}`,
    name,
    budget: 500,
    goalkeepers: 0,
    defenders: 0,
    midfielders: 0,
    forwards: 0,
  }))
  const state = {
    id: sid,
    leagueId: league.id,
    leagueSeasonId: league.leagueSeasonId,
    listVersionId: 'list-1',
    status: 'Active',
    version: 1,
    currentTeamId: options.waiting ? 'team-0' : 'team-1',
    teamOrder: teams.map((t) => t.id),
    teams,
    currentAuction: options.waiting
      ? null
      : {
          id: aid,
          playerId,
          name: 'Alessandro Fabbri',
          role: 'A',
          clubName: 'Demo Aurora',
          callerTeamId: 'team-1',
          winningTeamId: 'team-1',
          currentAmount: 20,
          durationSeconds: 30,
          increments: [1, 5, 10],
          deadline: new Date(Date.now() + 30000).toISOString(),
          status: 'Open',
          startedAt: new Date().toISOString(),
          closedAt: null,
        },
    serverTime: new Date().toISOString(),
  }
  const control = {
    reject: false,
    uncertain: false,
    expired: false,
    commands: [] as Record<string, unknown>[],
    receiptReads: 0,
  }
  const receipts = new Map<string, unknown>()
  let socket: WebSocketRoute | undefined
  const snapshot = () => ({ ...state, serverTime: new Date().toISOString() })
  const notify = () =>
    socket?.send(
      JSON.stringify({
        type: 1,
        target: 'AuctionChanged',
        arguments: [{ sessionId: sid, version: state.version }],
      }) + '\x1e',
    )
  await page.route(
    'http://localhost:6060/hubs/Auctions/negotiate?**',
    (route) =>
      route.fulfill({
        json: {
          negotiateVersion: 1,
          connectionId: 'test',
          connectionToken: 'test',
          availableTransports: [
            { transport: 'WebSockets', transferFormats: ['Text'] },
          ],
        },
      }),
  )
  await page.routeWebSocket('ws://localhost:6060/hubs/Auctions?**', (ws) => {
    socket = ws
    ws.onMessage((message) => {
      for (const part of String(message).split('\x1e').filter(Boolean)) {
        const value = JSON.parse(part) as {
          protocol?: string
          target?: string
          invocationId?: string
        }
        if (value.protocol) ws.send('{}\x1e')
        if (value.target === 'WatchSession')
          ws.send(
            JSON.stringify({
              type: 3,
              invocationId: value.invocationId,
              result: snapshot(),
            }) + '\x1e',
          )
        if (value.target === 'UnwatchSession')
          ws.send(
            JSON.stringify({ type: 3, invocationId: value.invocationId }) +
              '\x1e',
          )
      }
    })
  })
  await page.route('http://localhost:6060/api/**', async (route) => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    const respond = (data: unknown, status = 200) =>
      route.fulfill({
        status,
        json: {
          isSuccess: status < 400,
          data,
          errors:
            status < 400
              ? []
              : [
                  {
                    code: 'auction.rejected',
                    message: 'Offerta superata da un altro partecipante.',
                  },
                ],
        },
      })
    if (path.endsWith('/Antiforgery')) return respond({ token: 'csrf-test' })
    if (control.expired) return respond(null, 401)
    if (path.endsWith('/Me')) return respond(user)
    if (path.endsWith('/Leagues/league-1')) return respond(league)
    if (path.endsWith('/AuctionRoom'))
      return respond({
        leagueId: league.id,
        leagueSeasonId: league.leagueSeasonId,
        myTeamId: 'team-0',
        canManage: !!options.organizer,
        sessionId: roomSessionId,
        listVersionId: 'list-1',
        teams,
      })
    if (path.endsWith(`/Sessions/${sid}`)) return respond(snapshot())
    if (path.includes('/Commands/')) {
      control.receiptReads++
      const receipt = receipts.get(path.split('/').at(-1)!)
      return respond(receipt ?? null, receipt ? 200 : 404)
    }
    if (request.method() === 'POST') {
      expect(request.headers()['x-xsrf-token']).toBe('csrf-test')
      const body = request.postDataJSON() as Record<string, unknown>
      control.commands.push(body)
      if (path.endsWith('/Sessions')) {
        roomSessionId = sid
        return respond(snapshot(), 201)
      }
      if (!control.reject) {
        state.version++
        if (path.endsWith('/Bids') && state.currentAuction) {
          state.currentAuction.currentAmount = Number(body.amount)
          state.currentAuction.winningTeamId = 'team-0'
        }
        if (path.endsWith('/Control'))
          state.status = body.action === 'Pause' ? 'Paused' : 'Active'
      }
      const receipt = {
        requestId: body.requestId,
        sessionId: sid,
        auctionId: aid,
        version: state.version,
        serverTime: new Date().toISOString(),
        accepted: !control.reject,
        statusCode: control.reject ? 409 : 200,
        message: control.reject
          ? 'Offerta superata da un altro partecipante.'
          : null,
      }
      receipts.set(String(body.requestId), receipt)
      notify()
      if (control.uncertain) return route.abort('failed')
      return respond(receipt, receipt.statusCode)
    }
    if (path.endsWith('/Catalog'))
      return respond({
        items: [
          {
            playerId,
            name: 'Alessandro Fabbri',
            role: 'A',
            clubName: 'Demo Aurora',
            isAvailable: true,
            teamId: null,
          },
        ],
        total: 1,
        page: 1,
        pageSize: 30,
      })
    if (path.endsWith('/Roster') || path.endsWith('/Bids'))
      return respond({ items: [], total: 0, page: 1, pageSize: 100 })
    return route.fulfill({ status: 404 })
  })
  await page.goto(url)
  if (options.setup)
    await expect(
      page.getByRole('heading', {
        name: options.organizer ? 'Prepariamo l’asta.' : 'Ci siamo quasi.',
      }),
    ).toBeVisible()
  else
    await expect(
      page.getByText('Connesso alla sala', { exact: true }),
    ).toBeVisible()
  return { state, control, notify }
}

test('preparazione: l’organizzatore riordina le squadre e apre la sessione', async ({
  page,
}) => {
  const { control } = await setupRoom(page, {
    setup: true,
    organizer: true,
    waiting: true,
  })
  await page
    .getByRole('button', { name: 'Sposta su Real Sbronzi', exact: true })
    .click()
  await page
    .getByRole('button', { name: 'Apri la sessione d’asta', exact: true })
    .click()
  await expect(
    page.getByText('Connesso alla sala', { exact: true }),
  ).toBeVisible()
  expect(control.commands).toEqual([
    {
      leagueId: league.id,
      leagueSeasonId: league.leagueSeasonId,
      teamOrder: ['team-1', 'team-0', 'team-2', 'team-3'],
    },
  ])
})

test('preparazione: il partecipante attende l’avvio senza comandi amministrativi', async ({
  page,
}) => {
  await setupRoom(page, { setup: true })
  await expect(
    page.getByRole('button', { name: 'Apri la sessione d’asta', exact: true }),
  ).toHaveCount(0)
  await expect(page.locator('.team-order-editor')).toHaveCount(0)
})

for (const width of [1440, 390, 320]) {
  test(`sala navigabile e rilanci accessibili a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: width === 320 ? 568 : 900 })
    await setupRoom(page)
    await expect(page.locator('.auction-stage h2')).toHaveText(
      'Alessandro Fabbri',
    )
    const button = page.getByRole('button', {
      name: 'Offri 21 crediti, più 1',
      exact: true,
    })
    await expect(button).toBeEnabled()
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    if (width < 650) {
      await page.evaluate(() => window.scrollTo(0, 500))
      const box = await page.locator('.auction-side--bidding').boundingBox()
      expect(box!.y).toBeGreaterThan(0)
      expect(box!.y + box!.height).toBeLessThanOrEqual(
        (await page.evaluate(() => innerHeight)) + 1,
      )
      await expect(button).toBeInViewport()
    }
    await page.getByRole('tab', { name: 'Listone' }).focus()
    await page.keyboard.press('ArrowRight')
    await expect(page.getByRole('tab', { name: 'La mia rosa' })).toBeFocused()
    await expect(
      page.getByRole('tab', { name: 'La mia rosa' }),
    ).toHaveAttribute('aria-selected', 'true')
    await page.screenshot({
      path: test.info().outputPath(`auction-${width}.png`),
      fullPage: true,
    })
  })
}

test('rilancio rifiutato dal server conserva prezzo e consente un nuovo tentativo', async ({
  page,
}) => {
  const { control } = await setupRoom(page)
  control.reject = true
  await page.getByRole('button', { name: 'Offri 21 crediti, più 1' }).click()
  await expect(page.locator('.auction-command-status')).toContainText(
    'Offerta superata',
  )
  await expect(
    page.getByRole('button', { name: 'Offri 21 crediti, più 1' }),
  ).toBeEnabled()
  expect(control.commands).toHaveLength(1)
})

test('una risposta persa si recupera dalla ricevuta senza duplicare il rilancio', async ({
  page,
}) => {
  const { control } = await setupRoom(page)
  control.uncertain = true
  await page.getByRole('button', { name: 'Offri 21 crediti, più 1' }).click()
  await expect(
    page.getByRole('button', { name: 'Verifica esito' }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Verifica esito' }).click()
  await expect(
    page.getByRole('button', { name: 'Verifica esito' }),
  ).toHaveCount(0)
  await expect(page.locator('.auction-winner')).toContainText('Sei in testa')
  expect(control.commands).toHaveLength(1)
  expect(control.receiptReads).toBeGreaterThan(0)
})

test('perdita di connessione blocca i rilanci fino alla nuova sincronizzazione', async ({
  page,
}) => {
  await setupRoom(page)
  await page.evaluate(() => window.dispatchEvent(new Event('offline')))
  await expect(
    page.getByRole('button', { name: 'Offri 21 crediti, più 1' }),
  ).toBeDisabled()
  await page.evaluate(() => window.dispatchEvent(new Event('online')))
  await expect(
    page.getByText('Connesso alla sala', { exact: true }),
  ).toBeVisible()
  await expect(
    page.getByRole('button', { name: 'Offri 21 crediti, più 1' }),
  ).toBeEnabled()
})

test('l’organizzatore mette in pausa e riprende tra due chiamate', async ({
  page,
}) => {
  const { control } = await setupRoom(page, { organizer: true, waiting: true })
  await page.getByRole('button', { name: 'Gestisci asta' }).click()
  await page.getByRole('button', { name: 'Pausa', exact: true }).click()
  await expect(
    page.getByText('Sessione in pausa', { exact: true }),
  ).toBeVisible()
  await page.getByRole('button', { name: 'Riprendi', exact: true }).click()
  await expect(page.getByText('Sessione aperta', { exact: true })).toBeVisible()
  expect(control.commands.map((command) => command.action)).toEqual([
    'Pause',
    'Resume',
  ])
})

test('il partecipante può chiamare solo nel proprio turno e non gestisce l’asta', async ({
  page,
}) => {
  const { state, notify } = await setupRoom(page, { waiting: true })
  await expect(page.getByRole('button', { name: 'Gestisci asta' })).toHaveCount(
    0,
  )
  await page.getByRole('button', { name: 'Chiama Alessandro Fabbri' }).click()
  await expect(page.getByLabel('Timer', { exact: true })).toHaveValue('15')
  state.currentTeamId = 'team-1'
  state.version++
  notify()
  await expect(
    page.getByRole('button', { name: 'Chiama a 1 credito', exact: true }),
  ).toHaveCount(0)
})

test('una sessione scaduta durante l’asta torna al login', async ({ page }) => {
  const { state, control, notify } = await setupRoom(page)
  control.expired = true
  state.version++
  notify()
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.locator('.auction-stage')).toHaveCount(0)
})
