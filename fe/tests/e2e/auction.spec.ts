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
  options: {
    organizer?: boolean
    waiting?: boolean
    setup?: boolean
    catalogSize?: number
    teamCount?: number
  } = {},
) {
  let roomSessionId: string | null = options.setup ? null : sid
  const teams = [
    'Atletico Spritz',
    'Real Sbronzi',
    'Dinamo Divano',
    'Sporting Aperitivo',
    'Bayern Leverdure',
    'Borussia Porcelli',
    'AC Picchia',
    'FC Mai una Gioia',
  ]
    .slice(0, options.teamCount ?? 4)
    .map((name, i) => ({
      id: `team-${i}`,
      name,
      budget: 500,
      goalkeepers: 0,
      defenders: 0,
      midfielders: 0,
      forwards: 0,
    }))
  const participants = teams.map((team, index) => ({
    userId: `user-${index + 1}`,
    displayName:
      ['Demo', 'Luca Ferri', 'Giulia Rossi', 'Marco Bianchi'][index] ??
      `Utente ${index + 1}`,
    teamName: team.name,
    isOrganizer: index === 0,
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
    rosterReads: 0,
  }
  const receipts = new Map<string, unknown>()
  let socket: WebSocketRoute | undefined
  const snapshot = () => ({ ...state, serverTime: new Date().toISOString() })
  const presence = (
    connectedUsers: number,
    users = participants.slice(0, connectedUsers),
  ) =>
    socket?.send(
      JSON.stringify({
        type: 1,
        target: 'AuctionPresenceChanged',
        arguments: [{ sessionId: sid, connectedUsers, users }],
      }) + '\x1e',
    )
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
        participants,
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
        if (path.endsWith('/Players')) {
          state.currentAuction = {
            id: aid,
            playerId: String(body.playerId),
            name: 'Alessandro Fabbri',
            role: 'A',
            clubName: 'Demo Aurora',
            callerTeamId: 'team-0',
            winningTeamId: 'team-0',
            currentAmount: 1,
            durationSeconds: Number(body.durationSeconds),
            increments: body.increments as number[],
            deadline: new Date(
              Date.now() + Number(body.durationSeconds) * 1000,
            ).toISOString(),
            status: 'Open',
            startedAt: new Date().toISOString(),
            closedAt: null,
          }
        }
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
        items: Array.from({ length: options.catalogSize ?? 1 }, (_, index) => ({
          playerId: index === 0 ? playerId : `player-${index}`,
          name: index === 0 ? 'Alessandro Fabbri' : `Calciatore ${index + 1}`,
          role: 'A',
          clubName: 'Demo Aurora',
          currentQuotation: 17,
          initialQuotation: 16,
          fvm: 57,
          isAvailable: true,
          teamId: null,
        })),
        total: options.catalogSize ?? 1,
        page: 1,
        pageSize: 30,
      })
    if (path.endsWith('/Roster')) control.rosterReads++
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
  return { state, control, notify, presence, participants }
}

test('il Live desktop affianca il listone e dispone le squadre sotto il giocatore', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 })
  await setupRoom(page)
  await expect(page.getByLabel('Cerca calciatore')).toBeVisible()
  const player = (await page
    .getByRole('region', { name: 'Asta corrente', exact: true })
    .boundingBox())!
  const catalog = (await page.getByLabel('Cerca calciatore').boundingBox())!
  const board = (await page
    .getByRole('region', { name: 'Tabellone delle squadre' })
    .boundingBox())!
  expect(catalog.x).toBeGreaterThan(player.x + player.width)
  expect(board.y).toBeGreaterThan(player.y + player.height)
})

test('il listone arriva alla barra inferiore e mantiene la paginazione visibile', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 })
  await setupRoom(page, { catalogSize: 60 })
  const surface = page.locator('.auction-catalog-surface')
  const footer = page.locator('.auction-bottom-bar')
  async function expectAligned() {
    await expect
      .poll(async () => {
        const panel = (await surface.boundingBox())!
        const bar = (await footer.boundingBox())!
        return Math.abs(panel.y + panel.height + 16 - bar.y)
      })
      .toBeLessThanOrEqual(2)
    const pagination = (await page
      .getByRole('navigation', { name: 'Pagine del listone' })
      .boundingBox())!
    const bar = (await footer.boundingBox())!
    expect(pagination.y + pagination.height).toBeLessThanOrEqual(bar.y)
  }
  await expectAligned()
  await page.locator('.catalog-list').evaluate((list) => {
    list.scrollTop = list.scrollHeight
  })
  await expectAligned()
  await page.setViewportSize({ width: 1440, height: 700 })
  await expectAligned()
  await page.evaluate(() => window.scrollTo(0, 100))
  await expectAligned()
  await page.evaluate(() =>
    window.scrollTo(0, document.documentElement.scrollHeight),
  )
  await expectAligned()
  expect((await surface.boundingBox())!.y).toBeGreaterThanOrEqual(16)
  await expect(
    page.getByRole('heading', { name: 'Listone', exact: true }),
  ).toBeInViewport()
  await page.screenshot({
    path: test.info().outputPath('listone-bottom-gap.png'),
  })
})

test('il carosello squadre torna dalla prima all’ultima e viceversa', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 900 })
  await page.emulateMedia({ reducedMotion: 'reduce' })
  await setupRoom(page, { waiting: true })
  const board = page.getByLabel('Scorri le squadre', { exact: true })
  const previous = page.getByRole('button', { name: 'Squadre precedenti' })
  const next = page.getByRole('button', { name: 'Squadre successive' })
  const first = page.getByRole('button', { name: 'Apri rosa Atletico Spritz' })
  const last = page.getByRole('button', {
    name: 'Apri rosa Sporting Aperitivo',
  })
  const position = async (card: typeof first) =>
    Math.round((await card.boundingBox())!.x - (await board.boundingBox())!.x)
  await expect(previous).toBeEnabled()
  await expect(next).toBeEnabled()
  const firstPosition = await position(first)
  await previous.click()
  await expect.poll(() => position(last)).toBe(firstPosition)
  await next.click()
  await expect.poll(() => position(first)).toBe(firstPosition)
  await board.focus()
  await page.keyboard.press('End')
  await expect.poll(() => position(last)).toBe(firstPosition)
  await page.keyboard.press('Home')
  await expect.poll(() => position(first)).toBe(firstPosition)
  await expect(page.locator('.team-purchases')).toHaveCount(4)
  await page.screenshot({ path: test.info().outputPath('carousel-mobile.png') })
  await page.setViewportSize({ width: 1440, height: 1000 })
  await expect(previous).toBeDisabled()
  await expect(next).toBeDisabled()
})

test('il carosello taglia le card al bordo del box e supporta il drag', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 })
  await setupRoom(page, { waiting: true, teamCount: 8 })
  const board = page.getByLabel('Scorri le squadre', { exact: true })
  await board.scrollIntoViewIfNeeded()
  const section = page.getByRole('region', { name: 'Tabellone delle squadre' })
  const first = page.locator('.team-column').first()
  const last = page.locator('.team-column').last()
  await expect(
    page.getByRole('button', { name: 'Squadre precedenti' }),
  ).toBeEnabled()
  const viewport = (await board.boundingBox())!
  const box = (await section.boundingBox())!
  expect(Math.abs(viewport.x - box.x)).toBeLessThanOrEqual(1)
  expect(Math.abs(viewport.width - box.width)).toBeLessThanOrEqual(2)
  const previousCard = (await last.boundingBox())!
  expect(previousCard.x).toBeLessThan(viewport.x)
  expect(previousCard.x + previousCard.width).toBeGreaterThan(viewport.x + 12)
  const initialX = (await first.boundingBox())!.x
  const y = viewport.y + 50
  await page.mouse.move(initialX + 120, y)
  await page.mouse.down()
  await page.mouse.move(initialX - 80, y, { steps: 12 })
  await page.mouse.up()
  await expect
    .poll(async () => Math.abs((await first.boundingBox())!.x - initialX))
    .toBeGreaterThan(100)
  await expect(
    page.getByRole('tab', { name: 'Live', exact: true }),
  ).toHaveAttribute('aria-selected', 'true')
  await board.focus()
  await page.keyboard.press('Home')
  await expect
    .poll(async () => Math.round((await first.boundingBox())!.x))
    .toBe(Math.round(initialX))
  await page.mouse.move(initialX + 120, y)
  await page.mouse.wheel(150, 0)
  await expect
    .poll(async () => Math.abs((await first.boundingBox())!.x - initialX))
    .toBeGreaterThan(100)
  await board.focus()
  await page.keyboard.press('Home')
  await expect
    .poll(async () => Math.round((await first.boundingBox())!.x))
    .toBe(Math.round(initialX))
  await page.getByRole('heading', { name: 'Le squadre', exact: true }).click()
  await page.screenshot({
    path: test.info().outputPath('carousel-desktop.png'),
  })
})

test('selezione privata in anteprima: annulla senza offerte e chiama solo dopo conferma', async ({
  page,
}) => {
  const { control } = await setupRoom(page, { waiting: true })
  await page
    .getByRole('button', { name: 'Seleziona Alessandro Fabbri', exact: true })
    .click()
  const preview = page.getByRole('form', { name: 'Anteprima chiamata' })
  await expect(preview).toBeVisible()
  await expect(preview.getByLabel('Quotazioni del giocatore')).toContainText(
    '57',
  )
  await expect(
    preview.getByRole('heading', { name: 'Alessandro Fabbri' }),
  ).toBeVisible()
  expect(control.commands).toHaveLength(0)
  await preview.getByRole('button', { name: 'Annulla', exact: true }).click()
  await expect(preview).not.toBeVisible()
  expect(control.commands).toHaveLength(0)
  await page
    .getByRole('button', { name: 'Seleziona Alessandro Fabbri', exact: true })
    .click()
  await page
    .getByRole('group', { name: 'Timer', exact: true })
    .getByRole('button', { name: '20 secondi', exact: true })
    .click()
  const increments = page.getByRole('group', {
    name: 'Incrementi dei rilanci',
    exact: true,
  })
  await increments.getByRole('button', { name: '+2', exact: true }).click()
  await increments.getByRole('button', { name: '+10', exact: true }).click()
  await page
    .getByRole('button', { name: 'Chiama a 1 credito', exact: true })
    .click()
  await expect(
    page
      .getByRole('region', { name: 'Asta corrente', exact: true })
      .getByRole('heading', { name: 'Alessandro Fabbri' }),
  ).toBeVisible()
  expect(control.commands).toHaveLength(1)
  expect(control.commands[0]).toMatchObject({
    playerId,
    durationSeconds: 20,
    increments: [1, 2, 5],
  })
})

test('le opzioni della chiamata restano compatte su mobile e richiedono almeno un incremento', async ({
  page,
}) => {
  await page.setViewportSize({ width: 320, height: 900 })
  const { control } = await setupRoom(page, { waiting: true })
  await page.getByRole('tab', { name: 'Listone', exact: true }).click()
  await page
    .getByRole('button', { name: 'Seleziona Alessandro Fabbri', exact: true })
    .click()
  const preview = page.getByRole('form', { name: 'Anteprima chiamata' })
  const increments = preview.getByRole('group', {
    name: 'Incrementi dei rilanci',
    exact: true,
  })
  await increments.getByRole('button', { name: '+5', exact: true }).click()
  await increments.getByRole('button', { name: '+10', exact: true }).click()
  await expect(
    increments.getByRole('button', { name: '+1', exact: true }),
  ).toBeDisabled()
  await expect(
    increments.getByRole('button', { name: '+1', exact: true }),
  ).toHaveAttribute('aria-pressed', 'true')
  expect(control.commands).toHaveLength(0)
  for (const option of await preview.locator('.call-option').all()) {
    const box = (await option.boundingBox())!
    expect(box.x).toBeGreaterThanOrEqual(0)
    expect(box.x + box.width).toBeLessThanOrEqual(320)
  }
  await page.screenshot({
    path: test.info().outputPath('call-options-mobile.png'),
    fullPage: true,
  })
})

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

for (const width of [1920, 1440, 768, 390, 320]) {
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
    await page.screenshot({ path: test.info().outputPath(`live-${width}.png`) })
    await page.getByRole('tab', { name: 'Listone' }).focus()
    await page.keyboard.press('ArrowRight')
    await expect(
      page.getByRole('tab', { name: 'Rose', exact: true }),
    ).toBeFocused()
    await expect(
      page.getByRole('tab', { name: 'Rose', exact: true }),
    ).toHaveAttribute('aria-selected', 'true')
    await page.screenshot({
      path: test.info().outputPath(`auction-${width}.png`),
      fullPage: true,
    })
  })
}

test('i rilanci non rileggono le rose, una nuova aggiudicazione le aggiorna', async ({
  page,
}) => {
  const { control, state, notify } = await setupRoom(page)
  await expect(page.locator('.team-purchases')).toHaveCount(4)
  await expect(
    page.locator('.team-purchases').getByText('Il primo acquisto ti aspetta'),
  ).toHaveCount(4)
  const reads = control.rosterReads
  await page.getByRole('button', { name: 'Offri 21 crediti, più 1' }).click()
  await expect(page.getByText('La tua squadra è in testa.')).toBeVisible()
  expect(control.rosterReads).toBe(reads)
  state.teams[0]!.forwards = 1
  state.version++
  notify()
  await expect.poll(() => control.rosterReads).toBeGreaterThan(reads)
})

test('il cambio turno mantiene un annuncio accessibile nella vista Live', async ({
  page,
}) => {
  const { state, notify } = await setupRoom(page, { waiting: true })
  const turn = page.getByRole('status', { name: 'Turno di chiamata' })
  await expect(turn).toHaveText('Tocca a te')
  state.currentTeamId = 'team-1'
  state.version++
  notify()
  await expect(turn).toHaveText('Chiama: Real Sbronzi')
})

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
  await page.setViewportSize({ width: 320, height: 740 })
  const { control } = await setupRoom(page, { organizer: true, waiting: true })
  await expect(page.getByRole('tab')).toHaveCount(5)
  await page.getByRole('tab', { name: 'Gestisci asta' }).click()
  await expect(
    page.getByRole('tabpanel', { name: 'Gestisci asta' }),
  ).toBeVisible()
  await page.keyboard.press('Tab')
  await expect(
    page.getByRole('tabpanel', { name: 'Gestisci asta' }),
  ).toBeFocused()
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true)
  await page.screenshot({
    path: test.info().outputPath('manage-mobile.png'),
    fullPage: true,
  })
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
  await expect(page.getByRole('tab', { name: 'Gestisci asta' })).toHaveCount(0)
  await page.getByRole('tab', { name: 'Listone' }).click()
  await page
    .getByRole('button', { name: 'Seleziona Alessandro Fabbri' })
    .click()
  await expect(
    page.getByRole('button', { name: '15 secondi', exact: true }),
  ).toHaveAttribute('aria-pressed', 'true')
  state.currentTeamId = 'team-1'
  state.version++
  notify()
  await expect(
    page.getByRole('button', { name: 'Chiama a 1 credito', exact: true }),
  ).toHaveCount(0)
})

test('la navigazione conserva il listone e aggiorna il riepilogo live senza inviare offerte', async ({
  page,
}) => {
  const { state, control, notify } = await setupRoom(page)
  await expect(
    page.getByRole('tab', { name: 'Live', exact: true }),
  ).toHaveAttribute('aria-selected', 'true')
  await page.getByRole('tab', { name: 'Listone' }).click()
  await page.keyboard.press('Tab')
  await expect(
    page.getByRole('tabpanel', { name: 'Listone', exact: true }),
  ).toBeFocused()
  await expect(
    page.getByRole('region', { name: 'Asta corrente', exact: true }),
  ).not.toBeVisible()
  await page.getByLabel('Cerca calciatore').fill('Fabbri')
  await page.getByRole('button', { name: 'A', exact: true }).click()
  await page.getByRole('tab', { name: 'Rose', exact: true }).click()
  state.currentAuction!.currentAmount = 25
  state.version++
  notify()
  const summary = page.getByRole('region', { name: 'Riepilogo asta in corso' })
  await expect(summary).toContainText('25 crediti')
  await expect(summary).toContainText('Alessandro Fabbri')
  await page.getByRole('tab', { name: 'Listone' }).click()
  await expect(page.getByLabel('Cerca calciatore')).toHaveValue('Fabbri')
  await expect(
    page.getByRole('button', { name: 'A', exact: true }),
  ).toHaveAttribute('aria-pressed', 'true')
  await page.getByRole('button', { name: 'Torna ai rilanci' }).click()
  await expect(
    page.getByRole('button', { name: 'Offri 26 crediti, più 1' }),
  ).toBeEnabled()
  expect(control.commands).toHaveLength(0)
})

test('scegliendo dal fondo del listone il form di chiamata diventa raggiungibile', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 844 })
  await setupRoom(page, { waiting: true, catalogSize: 30 })
  await page.getByRole('tab', { name: 'Listone' }).click()
  await page
    .getByRole('button', { name: 'Seleziona Calciatore 30', exact: true })
    .click()
  await expect(
    page.getByRole('button', { name: '15 secondi', exact: true }),
  ).toBeFocused()
  await expect(
    page.getByRole('button', { name: '15 secondi', exact: true }),
  ).toBeInViewport()
  await expect(
    page.getByRole('button', { name: 'Chiama a 1 credito', exact: true }),
  ).toBeInViewport()
})

test('il turno resta accessibile e il tabellone apre la rosa scelta', async ({
  page,
}) => {
  await setupRoom(page, { waiting: true })
  const turn = page.getByRole('status', { name: 'Turno di chiamata' })
  await expect(
    page.getByRole('region', { name: 'Asta corrente', exact: true }),
  ).toContainText('Tocca a te')
  await page.getByRole('button', { name: 'Apri rosa Real Sbronzi' }).click()
  await expect(
    page.getByRole('tab', { name: 'Rose', exact: true }),
  ).toHaveAttribute('aria-selected', 'true')
  await expect(page.getByLabel('Rosa della squadra')).toHaveValue('team-1')
  await expect(turn).toHaveClass('sr-only')
  await page.getByRole('tab', { name: 'Storico' }).click()
  await expect(
    page.getByText(
      'Gli acquisti compariranno qui dopo le prime aggiudicazioni.',
    ),
  ).toBeVisible()
  await expect(turn).toHaveClass('sr-only')
  await page.getByRole('tab', { name: 'Live', exact: true }).click()
  await page
    .getByRole('button', { name: 'Scegli dal listone', exact: true })
    .click()
  await expect(page.getByLabel('Cerca calciatore')).toBeFocused()
})

test('su mobile navigazione, riepilogo e rilanci restano separati e raggiungibili', async ({
  page,
}) => {
  await page.setViewportSize({ width: 320, height: 568 })
  await setupRoom(page)
  const tabs = page.getByRole('tablist', { name: 'Sezioni della sala d’asta' })
  await expect(tabs).toBeInViewport()
  const budgetBox = (await page
    .getByRole('region', { name: 'Il tuo budget', exact: true })
    .boundingBox())!
  const bidBox = (await page.locator('.auction-side--bidding').boundingBox())!
  expect(bidBox.y + bidBox.height).toBeLessThanOrEqual(budgetBox.y + 1)
  await page.getByRole('tab', { name: 'Listone' }).click()
  const summary = page.getByRole('region', { name: 'Riepilogo asta in corso' })
  await expect(summary).toBeInViewport()
  const summaryBox = (await summary.boundingBox())!
  expect(summaryBox.y + summaryBox.height).toBeLessThanOrEqual(budgetBox.y + 1)
  await page.getByRole('button', { name: 'Torna ai rilanci' }).click()
  await expect(
    page.getByRole('button', { name: 'Offri 21 crediti, più 1' }),
  ).toBeInViewport()
})

test('una sessione scaduta durante l’asta torna al login', async ({ page }) => {
  const { state, control, notify } = await setupRoom(page)
  control.expired = true
  state.version++
  notify()
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.locator('.auction-stage')).toHaveCount(0)
})

test('intestazione asta: logo centrato e presenze reali a destra', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 })
  const { presence } = await setupRoom(page)
  presence(2)
  const badge = page.getByRole('status', { name: 'Connessione alla sala' })
  await expect(badge).toContainText('Utenti connessi: 2')
  const logo = (await page.locator('.app-brand').boundingBox())!
  const live = (await badge.boundingBox())!
  expect(Math.abs(logo.x + logo.width / 2 - 720)).toBeLessThan(2)
  expect(live.x).toBeGreaterThan(logo.x + logo.width)
  presence(1)
  await expect(badge).toContainText('Utenti connessi: 1')
  await page.evaluate(() => window.dispatchEvent(new Event('offline')))
  await expect(badge).toContainText('Riconnessione')
  await expect(badge).not.toContainText('Utenti connessi: 1')
})

for (const width of [390, 1440]) {
  test(`il badge LIVE mostra utenti connessi e assenti in tempo reale a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 })
    const { presence, participants } = await setupRoom(page)
    const trigger = page.getByRole('button', {
      name: 'Mostra utenti della sala',
    })
    await trigger.click()
    const panel = page.getByRole('dialog', { name: 'Utenti della sala' })
    await expect(panel).toBeVisible()
    await expect(panel.getByText('Caricamento presenze…')).toBeVisible()
    presence(2)
    const online = panel.getByRole('region', { name: 'Connessi', exact: true })
    const offline = panel.getByRole('region', {
      name: 'Non connessi',
      exact: true,
    })
    await expect(online.getByText('Luca Ferri', { exact: true })).toBeVisible()
    await expect(
      online.getByText('Real Sbronzi', { exact: true }),
    ).toBeVisible()
    await expect(
      offline.getByText('Giulia Rossi', { exact: true }),
    ).toBeVisible()
    presence(2, [participants[0]!, participants[2]!])
    await expect(
      online.getByText('Giulia Rossi', { exact: true }),
    ).toBeVisible()
    await expect(offline.getByText('Luca Ferri', { exact: true })).toBeVisible()
    await expect(online.getByText('Luca Ferri', { exact: true })).toHaveCount(0)
    const box = (await panel.boundingBox())!
    expect(box.x).toBeGreaterThanOrEqual(0)
    expect(box.x + box.width).toBeLessThanOrEqual(width)
    await page.screenshot({
      path: test.info().outputPath(`presence-${width}.png`),
    })
    await page.keyboard.press('Escape')
    await expect(panel).not.toBeVisible()
    await trigger.click()
    await page.evaluate(() => window.dispatchEvent(new Event('offline')))
    await expect(
      panel.getByText(
        'Riconnessione in corso. Le presenze verranno aggiornate al ripristino.',
      ),
    ).toBeVisible()
    await expect(online).toHaveCount(0)
    await page.getByRole('heading', { name: 'Le squadre', exact: true }).click()
    await expect(panel).not.toBeVisible()
  })
}

test('il budget personale resta fisso e segue lo snapshot nelle altre sezioni', async ({
  page,
}) => {
  const { state, notify, control } = await setupRoom(page, { waiting: true })
  const budget = page.getByRole('region', {
    name: 'Il tuo budget',
    exact: true,
  })
  const progress = budget.getByRole('progressbar')
  await expect(budget).toContainText('Atletico Spritz')
  await expect(progress).toHaveAttribute('value', '500')
  await expect(progress).toHaveAttribute('max', '500')
  await expect(page.locator('#live-bid-controls')).toHaveCount(0)
  await page.getByRole('tab', { name: 'Rose', exact: true }).click()
  state.teams[0]!.budget = 250
  state.version++
  notify()
  await expect(progress).toHaveAttribute('value', '250')
  await expect(budget).toContainText('250')
  await page.setViewportSize({ width: 390, height: 844 })
  await expect(budget).toBeInViewport()
  state.teams[0]!.budget = 0
  state.version++
  notify()
  await expect(progress).toHaveAttribute('value', '0')
  expect(control.commands).toHaveLength(0)
})
