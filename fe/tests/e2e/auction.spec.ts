import { test, expect, type Page, type WebSocketRoute } from '@playwright/test'
import type {
  BombAuctionView,
  CatalogEntry,
} from '../../src/features/auctions/types/auction.types'

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
const teamId = (index: number) =>
  `10000000-0000-4000-8000-${String(index + 1).padStart(12, '0')}`
const url = '/leghe/league-1/asta'
async function setupRoom(
  page: Page,
  options: {
    organizer?: boolean
    waiting?: boolean
    setup?: boolean
    catalogSize?: number
    teamCount?: number
    currentRole?: 'P' | 'D' | 'C' | 'A'
    rosterRoles?: ('P' | 'D' | 'C' | 'A')[]
    catalogPlayer?: Partial<CatalogEntry>
  } = {},
) {
  await page.addInitScript(() => {
    const audioState = window as unknown as { auctionAudioPlays: number }
    audioState.auctionAudioPlays = 0
    HTMLMediaElement.prototype.play = function () {
      audioState.auctionAudioPlays++
      return Promise.resolve()
    }
  })
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
    'Paris San Gennaro',
    'AS Intomatici',
  ]
    .slice(0, options.teamCount ?? 4)
    .map((name, i) => ({
      id: teamId(i),
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
    currentRole: options.currentRole ?? 'A',
    currentTeamId: options.waiting ? teamId(0) : teamId(1),
    teamOrder: teams.map((t) => t.id),
    teams,
    currentBomb: null as BombAuctionView | null,
    currentAuction: options.waiting
      ? null
      : {
          id: aid,
          playerId,
          name: 'Alessandro Fabbri',
          role: options.currentRole ?? 'A',
          clubName: 'Demo Aurora',
          callerTeamId: teamId(1),
          winningTeamId: teamId(1),
          currentAmount: 20,
          durationSeconds: 30,
          increments: [1, 5, 10],
          deadline: new Date(Date.now() + 30000).toISOString(),
          status: 'Open',
          startedAt: new Date().toISOString(),
          closedAt: null as string | null,
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
    catalogRemoved: [] as string[],
    catalogRequests: [] as number[],
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
        myTeamId: teamId(0),
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
          state.currentBomb = null
          state.currentAuction = {
            id: aid,
            playerId: String(body.playerId),
            name: 'Alessandro Fabbri',
            role: 'A',
            clubName: 'Demo Aurora',
            callerTeamId: teamId(0),
            winningTeamId: teamId(0),
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
        if (path.endsWith('/Bombs'))
          state.currentBomb = {
            id: aid,
            playerId,
            name: 'Alessandro Fabbri',
            role: 'A',
            clubName: 'Demo Aurora',
            photoUrl: null,
            clubLogoUrl: null,
            callerTeamId: teamId(0),
            status: 'Waiting',
            round: 1,
            minimumAmount: 1,
            deadline: new Date(Date.now() + 60000).toISOString(),
            revealStartedAt: null,
            nextRevealAt: null,
            participants: teams.map((team) => ({
              teamId: team.id,
              hasSubmitted: false,
            })),
            revealedOffers: [],
            ownAmount: null,
            playerAuctionId: null,
            winningTeamId: null,
            winningAmount: null,
          }
        if (path.endsWith('/BombBids') && state.currentBomb) {
          state.currentBomb.ownAmount = Number(body.amount)
          state.currentBomb.participants[0]!.hasSubmitted = true
        }
        if (path.endsWith('/CancelBomb') && state.currentBomb)
          state.currentBomb.status = 'Cancelled'
        if (path.endsWith('/Bids') && state.currentAuction) {
          state.currentAuction.currentAmount = Number(body.amount)
          state.currentAuction.winningTeamId = teamId(0)
        }
        if (path.endsWith('/Control')) {
          if (body.action === 'Pause') state.status = 'Paused'
          if (body.action === 'Resume') state.status = 'Active'
          if (body.action === 'Complete') state.status = 'Completed'
          if (body.action === 'GoToTurn')
            state.currentTeamId = String(body.targetTeamId)
          if (body.action === 'Reorder')
            state.teamOrder = body.teamOrder as string[]
          if (body.action === 'SkipTurn')
            state.currentTeamId =
              state.teamOrder[
                (state.teamOrder.indexOf(state.currentTeamId) + 1) %
                  state.teamOrder.length
              ]!
        }
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
    if (path.endsWith('/Catalog')) {
      const requestedPage = Number(
        new URL(route.request().url()).searchParams.get('page') ?? 1,
      )
      control.catalogRequests.push(requestedPage)
      const items = Array.from(
        { length: options.catalogSize ?? 1 },
        (_, index) => ({
          playerId: index === 0 ? playerId : `player-${index}`,
          name: index === 0 ? 'Alessandro Fabbri' : `Calciatore ${index + 1}`,
          role: options.currentRole ? (index % 2 === 0 ? 'P' : 'D') : 'A',
          clubName: 'Demo Aurora',
          currentQuotation: 17,
          initialQuotation: 16,
          fvm: 57,
          isAvailable: true,
          teamId: null,
          ...options.catalogPlayer,
        }),
      ).filter((item) => !control.catalogRemoved.includes(item.playerId))
      return respond({
        items: items.slice((requestedPage - 1) * 30, requestedPage * 30),
        total: items.length,
        page: requestedPage,
        pageSize: 30,
      })
    }
    if (path.endsWith('/Roster')) {
      control.rosterReads++
      const requestedTeam = new URL(route.request().url()).searchParams.get(
        'teamId',
      )
      const requestedPage = Number(
        new URL(route.request().url()).searchParams.get('page') ?? 1,
      )
      const items = (
        requestedTeam && requestedTeam !== teamId(0)
          ? []
          : (options.rosterRoles ?? [])
      ).map((role, index) => ({
        playerId: `roster-player-${index}`,
        teamId: teamId(0),
        playerAuctionId: `auction-${index}`,
        name: `Calciatore ${index + 1}`,
        role,
        clubName: 'Demo Aurora',
        price: index + 1,
        acquiredAt: new Date().toISOString(),
      }))
      return respond({
        items: items.slice((requestedPage - 1) * 100, requestedPage * 100),
        total: items.length,
        page: requestedPage,
        pageSize: 100,
      })
    }
    if (path.endsWith('/Bids'))
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

for (const width of [320, 1440]) {
  test(`audio automatico riservato al gestore con toggle nella bottom bar a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    const { state, notify } = await setupRoom(page, { organizer: true })
    const count = () =>
      page.evaluate(
        () =>
          (window as unknown as { auctionAudioPlays: number })
            .auctionAudioPlays,
      )
    await expect.poll(count).toBe(1)
    const audio = page
      .locator('.auction-bottom-bar')
      .getByRole('button', { name: 'Disattiva audio asta' })
    await expect(audio).toBeVisible()
    const box = (await audio.boundingBox())!
    expect(box.x).toBeGreaterThanOrEqual(0)
    expect(box.x + box.width).toBeLessThanOrEqual(width)
    await audio.click()
    state.currentAuction!.currentAmount++
    state.version++
    notify()
    await expect(page.locator('.auction-price')).toContainText('21')
    expect(await count()).toBe(1)
    await page.getByRole('button', { name: 'Attiva audio asta' }).click()
    await expect.poll(count).toBe(2)
    await page.screenshot({
      path: test.info().outputPath(`audio-${width}.png`),
      fullPage: true,
    })
  })
}

test('il partecipante non riceve il controllo audio né suoni dei rilanci', async ({
  page,
}) => {
  await setupRoom(page)
  await expect(page.locator('.auction-audio-toggle')).toHaveCount(0)
  expect(
    await page.evaluate(
      () =>
        (window as unknown as { auctionAudioPlays: number }).auctionAudioPlays,
    ),
  ).toBe(0)
})

for (const organizer of [false, true]) {
  test(`celebrazione con fanfara per ${organizer ? 'gestore' : 'partecipante'} solo alla conferma`, async ({
    page,
  }) => {
    await page.setViewportSize({ width: organizer ? 1440 : 390, height: 900 })
    const { state, notify, control } = await setupRoom(page, { organizer })
    const plays = () =>
      page.evaluate(
        () =>
          (window as unknown as { auctionAudioPlays: number })
            .auctionAudioPlays,
      )
    await expect.poll(plays).toBe(organizer ? 1 : 0)
    Object.assign(state.currentAuction!, {
      status: 'Closed',
      closedAt: new Date().toISOString(),
    })
    state.version++
    notify()
    const celebration = page.getByRole('region', {
      name: 'Aggiudicazione',
      exact: true,
    })
    await expect(celebration).toContainText('Real Sbronzi')
    await expect(celebration).toContainText('Alessandro Fabbri')
    await expect.poll(plays).toBe(organizer ? 2 : 1)
    await expect(page.locator('.auction-victory-confetti svg')).toHaveCount(1)
    await expect(celebration).toHaveCSS('opacity', '1')
    await page.screenshot({
      path: test
        .info()
        .outputPath(`victory-${organizer ? 'desktop' : 'mobile'}.png`),
    })
    state.version++
    notify()
    await expect(celebration).toBeHidden({ timeout: 6000 })
    expect(await plays()).toBe(organizer ? 2 : 1)
    expect(control.commands).toHaveLength(0)
    await page.reload()
    await expect(
      page.getByRole('tab', { name: 'Live', exact: true }),
    ).toBeVisible()
    await expect(celebration).toBeHidden()
    expect(await plays()).toBe(0)
  })
}

test('l’anteprima mantiene 20 secondi e non invia rilanci reali', async ({
  page,
}) => {
  const { control, state, notify } = await setupRoom(page)
  await page.goto(`${url}?preview=timer`)
  await expect(page.getByRole('timer')).toHaveAttribute(
    'aria-label',
    '20 secondi rimasti',
  )
  await page.getByRole('button', { name: 'Offri 21 crediti, più 1' }).click()
  await expect(
    page.getByRole('region', { name: 'Squadra in testa' }),
  ).toContainText('Atletico Spritz')
  state.currentAuction!.status = 'Closed'
  state.version++
  notify()
  await page.clock.install()
  await page.clock.fastForward(60_000)
  await expect(page.getByRole('timer')).toHaveAttribute(
    'aria-label',
    '20 secondi rimasti',
  )
  expect(control.commands).toHaveLength(0)
  await page.getByRole('link', { name: 'Torna all’asta' }).click()
  await expect(page).toHaveURL(new RegExp(`${url}$`))
})

test('la prova vittoria è locale e con movimento ridotto mostra un riepilogo statico', async ({
  page,
}) => {
  await page.emulateMedia({ reducedMotion: 'reduce' })
  const { control } = await setupRoom(page)
  await page.goto(`${url}?preview=timer`)
  await page.getByRole('button', { name: 'Prova vittoria' }).click()
  const celebration = page.getByRole('region', {
    name: 'Aggiudicazione',
    exact: true,
  })
  await expect(celebration).toContainText('Real Sbronzi')
  await expect(celebration).toHaveCSS('animation-name', 'none')
  await expect(page.locator('.auction-victory-confetti svg')).toHaveCount(0)
  await page.getByRole('button', { name: 'Chiudi celebrazione' }).click()
  await page.getByRole('button', { name: 'Ripristina anteprima' }).click()
  await expect(page.getByRole('timer')).toHaveAttribute(
    'aria-label',
    '20 secondi rimasti',
  )
  expect(control.commands).toHaveLength(0)
})

for (const width of [320, 1440]) {
  test(`listone a card: layout, filtri e selezione a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    const { state, notify, control } = await setupRoom(page, {
      waiting: true,
      catalogSize: 4,
      catalogPlayer: { currentQuotation: 137, fvm: 445 },
    })
    if (width === 320) {
      const waiting = page.locator('.auction-waiting')
      const button = page.getByRole('button', {
        name: 'Scegli dal listone',
        exact: true,
      })
      const bounds = (await waiting.boundingBox())!
      const action = (await button.boundingBox())!
      expect(Math.abs(action.width - bounds.width)).toBeLessThanOrEqual(1)
      await expect(waiting.locator('.auction-waiting-copy')).toHaveCSS(
        'text-align',
        'center',
      )
      await page.screenshot({
        path: test.info().outputPath('waiting-centered-mobile.png'),
      })
    }
    await page.getByRole('tab', { name: 'Listone', exact: true }).click()
    const catalog = page.locator('.auction-catalog--expanded')
    await expect(
      page.getByRole('heading', { name: 'Listone', exact: true }),
    ).toHaveCount(0)
    const rows = catalog.locator('.catalog-row')
    await expect(rows).toHaveCount(4)
    await expect(catalog.getByText('Disponibile', { exact: true })).toHaveCount(
      0,
    )
    const first = (await rows.nth(0).boundingBox())!
    const second = (await rows.nth(1).boundingBox())!
    if (width === 1440) {
      expect(Math.abs(first.y - second.y)).toBeLessThan(2)
      expect(second.x).toBeGreaterThan(first.x)
    } else {
      expect(second.y).toBeGreaterThan(first.y)
      expect(
        await page.evaluate(() => document.documentElement.scrollWidth),
      ).toBeLessThanOrEqual(width)
    }
    const filters = catalog.locator('.role-filters button')
    for (let index = 1; index < (await filters.count()); index++) {
      const previous = (await filters.nth(index - 1).boundingBox())!
      const current = (await filters.nth(index).boundingBox())!
      expect(current.x).toBeGreaterThanOrEqual(previous.x + previous.width)
    }
    await expect(rows.first().getByText('137', { exact: true })).toBeVisible()
    await expect(rows.first().getByText('445', { exact: true })).toBeVisible()
    for (const value of await rows
      .first()
      .locator('.player-valuation dd')
      .all()) {
      expect((await value.boundingBox())!.height).toBeLessThan(24)
      expect(
        await value.evaluate((node) => node.scrollWidth <= node.clientWidth),
      ).toBe(true)
    }
    const choose = catalog.getByRole('button', {
      name: 'Seleziona Alessandro Fabbri',
      exact: true,
    })
    await expect(choose).toBeEnabled()
    await page.screenshot({
      path: test.info().outputPath(`catalog-cards-${width}.png`),
      fullPage: true,
    })
    state.currentTeamId = teamId(1)
    state.version++
    notify()
    await expect(choose).toBeDisabled()
    expect(control.commands).toHaveLength(0)
  })
}

for (const width of [320, 1440]) {
  test(`ordinamento listone conserva filtri e torna alla prima pagina a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    await setupRoom(page, { waiting: true, currentRole: 'P', catalogSize: 60 })
    await page.getByRole('tab', { name: 'Listone', exact: true }).click()
    if (width === 320) {
      const select = (await page
        .getByRole('combobox', { name: 'Ordina il listone' })
        .boundingBox())!
      const caption = (await page.locator('.catalog-caption').boundingBox())!
      expect(caption.y - select.y - select.height).toBeLessThan(40)
      expect(select.width).toBeGreaterThan(250)
    }
    const searchRequest = page.waitForRequest(
      (r) =>
        r.url().includes('/Catalog?') &&
        new URL(r.url()).searchParams.get('search') === 'Fabbri',
    )
    await page.getByLabel('Cerca calciatore').fill('Fabbri')
    await searchRequest
    const nextRequest = page.waitForRequest(
      (r) =>
        r.url().includes('/Catalog?') &&
        new URL(r.url()).searchParams.get('page') === '2',
    )
    await page
      .locator('.auction-catalog--expanded .infinite-scroll-more')
      .scrollIntoViewIfNeeded()
    await nextRequest
    for (const [value, label] of [
      ['fvm', 'Ordina: FVM ↓'],
      ['quotation', 'Ordina: Quotazione ↓'],
    ] as const) {
      const request = page.waitForRequest(
        (r) =>
          r.url().includes('/Catalog?') &&
          new URL(r.url()).searchParams.get('sort') === value,
      )
      await page.getByRole('combobox', { name: 'Ordina il listone' }).click()
      await page.getByRole('option', { name: label, exact: true }).click()
      const params = new URL((await request).url()).searchParams
      expect(params.get('page')).toBe('1')
      expect(params.get('role')).toBe('P')
      expect(params.get('search')).toBe('Fabbri')
      await expect(
        page.getByRole('combobox', { name: 'Ordina il listone' }),
      ).toHaveText(label)
    }
    await page.getByRole('combobox', { name: 'Ordina il listone' }).click()
    await page
      .getByRole('option', { name: 'Ordina: Nome', exact: true })
      .click()
    await expect(
      page.getByRole('combobox', { name: 'Ordina il listone' }),
    ).toHaveText('Ordina: Nome')
    await expect(page.getByLabel('Cerca calciatore')).toHaveValue('Fabbri')
    await expect(
      page.getByRole('button', { name: 'Precedente', exact: true }),
    ).toHaveCount(0)
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(width)
    await page.getByRole('tab', { name: 'Rose', exact: true }).click()
    await expect(
      page.getByRole('heading', { name: 'Rose', exact: true }),
    ).toHaveCount(0)
    await expect(
      page.getByText('Giocatori, budget e posti disponibili di ogni squadra.', {
        exact: true,
      }),
    ).toHaveCount(0)
  })
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

test('il listone laterale arriva alla barra inferiore e carica scorrendo', async ({
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
    await expect(
      page.getByRole('navigation', { name: 'Pagine del listone' }),
    ).toHaveCount(0)
  }
  await expectAligned()
  await page.locator('.catalog-list').evaluate((list) => {
    list.scrollTop = list.scrollHeight
  })
  await expect(page.locator('.auction-catalog .catalog-row')).toHaveCount(60)
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

test('test chiamante: mantiene il proprio turno, si disattiva e supporta il riordino drag', async ({
  page,
}) => {
  const { state, notify, control } = await setupRoom(page, {
    organizer: true,
    waiting: true,
  })
  await page.getByRole('tab', { name: 'Gestisci asta' }).click()
  await page.getByLabel('Test · Mantieni il turno alla mia squadra').check()
  state.currentTeamId = teamId(1)
  state.version++
  notify()
  await expect.poll(() => state.currentTeamId).toBe(teamId(0))
  expect(control.commands).toHaveLength(1)
  await page.getByLabel('Test · Mantieni il turno alla mia squadra').uncheck()
  state.currentTeamId = teamId(1)
  state.version++
  notify()
  await expect(
    page.getByRole('region', { name: 'Turno corrente', exact: true }),
  ).toContainText('Real Sbronzi')
  const rows = page.locator('.management-team')
  await rows.nth(2).scrollIntoViewIfNeeded()
  const handle = page.getByRole('button', { name: 'Trascina Atletico Spritz' })
  const start = (await handle.boundingBox())!
  const target = (await rows.nth(2).boundingBox())!
  await page.mouse.move(start.x + start.width / 2, start.y + start.height / 2)
  await page.mouse.down()
  await page.mouse.move(
    start.x + start.width / 2,
    target.y + target.height / 2,
    { steps: 12 },
  )
  await expect(rows.first()).toHaveAttribute('data-dragging', 'true')
  await expect
    .poll(() =>
      rows.nth(1).evaluate((element) => getComputedStyle(element).transform),
    )
    .not.toBe('none')
  expect(control.commands).toHaveLength(1)
  await page.screenshot({
    path: test.info().outputPath('management-dragging.png'),
  })
  await page.mouse.up()
  await expect(rows.nth(2)).toContainText('Atletico Spritz')
  expect(control.commands).toHaveLength(1)
  const cancel = page.getByRole('button', { name: 'Annulla modifiche' })
  await cancel.scrollIntoViewIfNeeded()
  const cancelBox = (await cancel.boundingBox())!
  await page.mouse.move(
    cancelBox.x + cancelBox.width / 2,
    cancelBox.y + cancelBox.height / 2,
    { steps: 12 },
  )
  await cancel.click()
  await expect(rows.first()).toContainText('Atletico Spritz')
  await handle.focus()
  await page.keyboard.press('Space', { delay: 100 })
  await expect(rows.first()).toHaveAttribute('data-dragging', 'true')
  await page.keyboard.press('ArrowDown', { delay: 100 })
  await expect
    .poll(() =>
      rows
        .nth(1)
        .evaluate(
          (element) => new DOMMatrix(getComputedStyle(element).transform).m42,
        ),
    )
    .toBeLessThan(-10)
  await page.keyboard.press('Escape')
  await expect(rows.first()).toHaveAttribute('data-dragging', 'false')
  await expect(rows.first()).toContainText('Atletico Spritz')
  await expect(handle).toBeFocused()
  await expect(page.getByRole('button', { name: 'Salva ordine' })).toHaveCount(
    0,
  )
  await page.keyboard.press('Space', { delay: 100 })
  await expect(rows.first()).toHaveAttribute('data-dragging', 'true')
  await page.keyboard.press('ArrowDown', { delay: 100 })
  await expect
    .poll(() =>
      rows
        .nth(1)
        .evaluate(
          (element) => new DOMMatrix(getComputedStyle(element).transform).m42,
        ),
    )
    .toBeLessThan(-10)
  await page.keyboard.press('Space')
  await expect(rows.nth(1)).toContainText('Atletico Spritz')
  expect(control.commands).toHaveLength(1)
})

test('gestione: trascinamento touch dalla maniglia senza salvare automaticamente', async ({
  page,
}) => {
  await page.setViewportSize({ width: 390, height: 1000 })
  const { control } = await setupRoom(page, { organizer: true, waiting: true })
  await page.getByRole('tab', { name: 'Gestisci asta' }).click()
  const rows = page.locator('.management-team')
  await rows
    .first()
    .evaluate((element) => element.scrollIntoView({ block: 'center' }))
  const handle = (await page
    .getByRole('button', { name: 'Trascina Atletico Spritz' })
    .boundingBox())!
  const target = (await rows.nth(1).boundingBox())!
  const x = handle.x + handle.width / 2
  const y = handle.y + handle.height / 2
  const cdp = await page.context().newCDPSession(page)
  await cdp.send('Input.dispatchTouchEvent', {
    type: 'touchStart',
    touchPoints: [{ x, y }],
  })
  for (let step = 1; step <= 12; step++) {
    await cdp.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [
        { x, y: y + ((target.y + target.height / 2 - y) * step) / 12 },
      ],
    })
  }
  await expect(rows.first()).toHaveAttribute('data-dragging', 'true')
  await cdp.send('Input.dispatchTouchEvent', {
    type: 'touchEnd',
    touchPoints: [],
  })
  await expect(rows.nth(1)).toContainText('Atletico Spritz')
  expect(control.commands).toHaveLength(0)
  await expect(page.getByRole('button', { name: 'Salva ordine' })).toBeVisible()
  await cdp.detach()
})

for (const width of [390, 1440]) {
  test(`la squadra in testa è in evidenza e le offerte sono sotto a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    const { state, notify } = await setupRoom(page)
    const leader = page.getByRole('region', {
      name: 'Squadra in testa',
      exact: true,
    })
    await expect(leader).toContainText('Real Sbronzi')
    const contest = (await page.locator('.auction-contest').boundingBox())!
    const offer = (await page.locator('.auction-current-offer').boundingBox())!
    expect(offer.y).toBeGreaterThanOrEqual(contest.y + contest.height)
    const quick = (await page.locator('.bid-quick').boundingBox())!
    const custom = (await page.locator('.bid-custom').boundingBox())!
    expect(custom.y).toBeGreaterThanOrEqual(quick.y + quick.height)
    await page.screenshot({
      path: test.info().outputPath(`leader-other-${width}.png`),
      fullPage: true,
    })
    state.currentAuction!.winningTeamId = teamId(0)
    state.currentAuction!.currentAmount = 26
    state.version++
    notify()
    await expect(leader).toContainText('Atletico Spritz')
    await expect(leader).toContainText('Sta vincendo:')
    await expect(page.getByText('La tua squadra è in testa.')).toHaveCount(0)
    await expect(
      page.getByRole('button', { name: 'Offri 27 crediti, più 1' }),
    ).toBeDisabled()
    await page.screenshot({
      path: test.info().outputPath(`leader-mine-${width}.png`),
      fullPage: true,
    })
  })
}

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
  await preview
    .getByRole('button', { name: 'Annulla selezione', exact: true })
    .click()
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
  await page.getByRole('button', { name: 'Chiama', exact: true }).click()
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

for (const [width, role] of [
  [1920, 'P'],
  [1440, 'D'],
  [1024, 'C'],
  [768, 'A'],
  [390, 'P'],
  [320, 'A'],
] as const) {
  test(`selezione ${role}: scheda e comandi leggibili a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    const name =
      width === 320 ? 'Un nome di calciatore particolarmente lungo' : 'Caprile'
    const { control } = await setupRoom(page, {
      waiting: true,
      currentRole: role,
      catalogPlayer: {
        name,
        role,
        currentQuotation: width === 320 ? null : 11,
        initialQuotation: width === 320 ? null : 9,
        fvm: width === 320 ? null : 25,
      },
    })
    if (width < 1024)
      await page.getByRole('tab', { name: 'Listone', exact: true }).click()
    await page
      .getByRole('button', { name: `Seleziona ${name}`, exact: true })
      .click()
    const preview = page.getByRole('form', { name: 'Anteprima chiamata' })
    const card = preview.getByRole('region', { name: 'Giocatore selezionato' })
    const panel = preview.getByRole('region', {
      name: 'Impostazioni e chiamata',
    })
    await expect(card.getByRole('heading', { name })).toBeVisible()
    await expect(card.getByRole('definition')).toHaveCount(4)
    const cardBox = (await card.boundingBox())!
    const panelBox = (await panel.boundingBox())!
    if (width >= 1440) {
      expect(panelBox.x).toBeGreaterThanOrEqual(cardBox.x + cardBox.width)
      expect(Math.abs(panelBox.y - cardBox.y)).toBeLessThan(2)
    } else {
      expect(panelBox.y).toBeGreaterThanOrEqual(cardBox.y + cardBox.height)
    }
    expect(
      await preview.evaluate((el) => el.scrollWidth <= el.clientWidth),
    ).toBe(true)
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    const submit = panel.getByRole('button', {
      name: 'Chiama',
      exact: true,
    })
    await submit.scrollIntoViewIfNeeded()
    await expect(submit).toBeInViewport()
    await page.screenshot({
      path: test.info().outputPath(`selection-${width}.png`),
      fullPage: true,
    })
    await preview.getByRole('button', { name: 'Annulla selezione' }).click()
    await expect(preview).not.toBeVisible()
    expect(control.commands).toHaveLength(0)
  })
}

test('preparazione: l’organizzatore riordina le squadre e apre la sessione', async ({
  page,
}) => {
  const { control } = await setupRoom(page, {
    setup: true,
    organizer: true,
    waiting: true,
  })
  await expect(page.getByRole('banner')).toBeVisible()
  await expect(
    page.getByText('Utenti connessi: 0', { exact: true }),
  ).toBeVisible()
  await page
    .getByRole('button', { name: 'Sposta su Real Sbronzi', exact: true })
    .click()
  await page
    .getByRole('button', { name: 'Apri la sessione d’asta', exact: true })
    .click()
  await expect(
    page.getByText('Connesso alla sala', { exact: true }),
  ).toBeVisible()
  await expect(page.getByRole('banner')).toBeVisible()
  expect(control.commands).toEqual([
    {
      leagueId: league.id,
      leagueSeasonId: league.leagueSeasonId,
      teamOrder: [teamId(1), teamId(0), teamId(2), teamId(3)],
    },
  ])
})

test('preparazione: il partecipante attende l’avvio senza comandi amministrativi', async ({
  page,
}) => {
  await setupRoom(page, { setup: true })
  await expect(page.getByRole('banner')).toBeVisible()
  await expect(
    page.getByText('Utenti connessi: 0', { exact: true }),
  ).toBeVisible()
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
  await expect(page.getByText('La tua squadra è in testa.')).toHaveCount(0)
  await expect(
    page.getByRole('region', { name: 'Squadra in testa', exact: true }),
  ).toContainText('Atletico Spritz')
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
  state.currentTeamId = teamId(1)
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
  await expect(page.locator('.auction-leader')).toContainText('Sta vincendo:')
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
    page.getByRole('button', { name: 'Disattiva audio asta' }),
  ).toBeFocused()
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

for (const width of [390, 1440]) {
  test(`gestione: salto diretto, riepilogo e ordine salvato a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    const { state, notify, control } = await setupRoom(page, {
      organizer: true,
      waiting: true,
      teamCount: 8,
    })
    await page.getByRole('tab', { name: 'Gestisci asta' }).click()
    await expect(page.locator('.auction-management-panel')).toHaveCSS(
      'max-width',
      '1180px',
    )
    await expect(page.locator('.auction-management-panel')).toHaveCSS(
      'scroll-margin-top',
      '140px',
    )
    const current = page.getByRole('region', {
      name: 'Turno corrente',
      exact: true,
    })
    await expect(current).toContainText('Atletico Spritz')
    const jump = page.getByRole('combobox', {
      name: 'Vai al turno di',
      exact: true,
    })
    await jump.click()
    await expect(page.getByRole('listbox')).toBeVisible()
    expect(control.commands).toHaveLength(0)
    await page.keyboard.press('Escape')
    await expect(jump).toBeFocused()
    await jump.click()
    await page.screenshot({
      path: test.info().outputPath(`management-select-${width}.png`),
    })
    await page
      .getByRole('option', { name: 'Real Sbronzi', exact: true })
      .click()
    await expect(current).toContainText('Real Sbronzi')
    expect(control.commands[0]).toMatchObject({
      action: 'GoToTurn',
      targetTeamId: teamId(1),
    })
    const rows = page.locator('.management-team')
    await page.getByRole('button', { name: 'Sposta su Real Sbronzi' }).click()
    await expect(rows.first()).toContainText('Real Sbronzi')
    expect(control.commands).toHaveLength(1)
    await page.getByRole('button', { name: 'Salva ordine' }).click()
    await expect(
      page.getByRole('button', { name: 'Salva ordine' }),
    ).toBeHidden()
    expect(control.commands[1]).toMatchObject({ action: 'Reorder' })
    expect(state.currentTeamId).toBe(teamId(1))
    await page.screenshot({
      path: test.info().outputPath(`management-${width}.png`),
      fullPage: true,
    })
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    state.currentAuction = {
      id: aid,
      playerId,
      name: 'Alessandro Fabbri',
      role: 'A',
      clubName: 'Demo Aurora',
      callerTeamId: teamId(1),
      winningTeamId: teamId(1),
      currentAmount: 1,
      durationSeconds: 30,
      increments: [1],
      deadline: new Date(Date.now() + 30_000).toISOString(),
      status: 'Open',
      startedAt: new Date().toISOString(),
      closedAt: null,
    }
    state.version++
    notify()
    await expect(
      page.getByRole('button', { name: 'Salta turno', exact: true }),
    ).toBeDisabled()
    await expect(
      page.getByRole('combobox', { name: 'Vai al turno di', exact: true }),
    ).toBeDisabled()
  })
}

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
  state.currentTeamId = teamId(1)
  state.version++
  notify()
  await expect(
    page.getByRole('button', { name: 'Chiama', exact: true }),
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
    page.getByRole('button', { name: 'Chiama', exact: true }),
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
  await expect(
    page.getByRole('article', { name: 'Rosa Real Sbronzi', exact: true }),
  ).toBeFocused()
  await expect(turn).toHaveClass('sr-only')
  await page.getByRole('tab', { name: 'Storico' }).click()
  await expect(
    page.getByText(
      'Gli acquisti compariranno qui dopo le prime aggiudicazioni.',
    ),
  ).toBeVisible()
  await expect(turn).toHaveClass('sr-only')
  const historyTab = page.getByRole('tab', { name: 'Storico', exact: true })
  await historyTab.focus()
  await historyTab.press('ArrowLeft')
  const rosterTab = page.getByRole('tab', { name: 'Rose', exact: true })
  await expect(rosterTab).toBeFocused()
  await rosterTab.press('ArrowRight')
  await expect(historyTab).toBeFocused()
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

for (const width of [320, 390, 768, 1440]) {
  test(`intestazione asta: lega e squadra accanto al logo a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 900 })
    const { presence } = await setupRoom(page)
    presence(2)
    const badge = page.getByRole('status', { name: 'Connessione alla sala' })
    await expect(badge).toContainText('Utenti connessi: 2')
    const logo = (await page.locator('.app-brand').boundingBox())!
    const live = (await badge.boundingBox())!
    if (width > 650)
      expect(Math.abs(logo.x + logo.width / 2 - width / 2)).toBeLessThan(2)
    expect(live.x).toBeGreaterThan(logo.x + logo.width)
    const header = page.locator('.app-header')
    if (width <= 650) {
      const profile = (await page
        .getByRole('button', { name: 'Apri menu profilo' })
        .boundingBox())!
      expect(logo.x).toBeGreaterThanOrEqual(profile.x + profile.width)
      expect(
        Math.abs(logo.y + logo.height / 2 - live.y - live.height / 2),
      ).toBeLessThan(2)
      expect((await header.boundingBox())!.height).toBeLessThanOrEqual(72)
    }
    await expect(
      header.locator('.auction-header-identity > strong'),
    ).toHaveText('Lega di prova')
    await expect(header.locator('.auction-header-identity > span')).toHaveText(
      'Atletico Spritz',
    )
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(width)
    await header.screenshot({
      path: test.info().outputPath(`header-${width}.png`),
    })
    presence(1)
    await expect(badge).toContainText('Utenti connessi: 1')
    await page.evaluate(() => window.dispatchEvent(new Event('offline')))
    await expect(badge).toContainText('Riconnessione')
    await expect(badge).not.toContainText('Utenti connessi: 1')
  })
}

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
  await expect(budget).not.toContainText('Atletico Spritz')
  await expect(progress).toHaveAttribute('value', '500')
  await expect(progress).toHaveAttribute('max', '500')
  await expect(page.locator('#live-bid-controls')).toHaveCount(0)
  await page.getByRole('tab', { name: 'Rose', exact: true }).click()
  state.teams[0]!.budget = 250
  state.version++
  notify()
  await expect(progress).toHaveAttribute('value', '250')
  await expect(budget).toContainText('250')
  await page
    .locator('.auction-bottom-bar')
    .screenshot({ path: test.info().outputPath('bottom-bar-desktop.png') })
  await page.setViewportSize({ width: 390, height: 844 })
  await expect(budget).toBeInViewport()
  await page
    .locator('.auction-bottom-bar')
    .screenshot({ path: test.info().outputPath('bottom-bar-mobile.png') })
  state.teams[0]!.budget = 0
  state.version++
  notify()
  await expect(progress).toHaveAttribute('value', '0')
  expect(control.commands).toHaveLength(0)
})

for (const width of [390, 1440]) {
  test(`acquisti nel carosello raggruppati in ordine P D C A a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    await setupRoom(page, {
      waiting: true,
      rosterRoles: ['A', 'C', 'P', 'D', 'P'],
    })
    const purchases = page.getByLabel('Acquisti Atletico Spritz', {
      exact: true,
    })
    await purchases.scrollIntoViewIfNeeded()
    await expect(purchases.getByRole('heading')).toHaveText([
      'Portieri',
      'Difensori',
      'Centrocampisti',
      'Attaccanti',
    ])
    await expect(
      purchases.locator('.team-purchase-details > span:first-child'),
    ).toHaveText([
      'Calciatore 3',
      'Calciatore 5',
      'Calciatore 4',
      'Calciatore 2',
      'Calciatore 1',
    ])
    expect((await purchases.boundingBox())!.height).toBeGreaterThanOrEqual(190)
    await purchases.evaluate((element) => {
      element.scrollTop = element.scrollHeight
    })
    await expect(
      purchases.getByText('Calciatore 1', { exact: true }),
    ).toBeInViewport()
    await purchases.evaluate((element) => {
      element.scrollTop = 0
    })
    await page.screenshot({
      path: test.info().outputPath(`roster-groups-${width}.png`),
    })
  })
}

for (const width of [390, 1440]) {
  test(`fase per ruolo: blocca altre chiamate e aggiorna il listone al passaggio a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    const { state, notify, control } = await setupRoom(page, {
      organizer: true,
      waiting: true,
      currentRole: 'P',
      catalogSize: 2,
    })
    await page.getByRole('tab', { name: 'Listone', exact: true }).click()
    await expect(
      page.getByRole('button', { name: 'P', exact: true }),
    ).toHaveAttribute('aria-pressed', 'true')
    await expect(
      page.getByRole('button', { name: 'Seleziona Alessandro Fabbri' }),
    ).toBeEnabled()
    await expect(
      page.getByRole('button', { name: 'Seleziona Calciatore 2' }),
    ).toBeDisabled()
    await page.getByRole('button', { name: 'Tutti', exact: true }).click()
    await expect(
      page.getByRole('button', { name: 'Seleziona Calciatore 2' }),
    ).toBeDisabled()
    await page
      .getByRole('button', { name: 'Seleziona Alessandro Fabbri' })
      .click()
    await expect(page.getByRole('button', { name: 'Chiama' })).toBeEnabled()
    state.teams.forEach((team) => {
      team.goalkeepers = league.goalkeepers
    })
    state.currentRole = 'D'
    state.version++
    notify()
    await expect(page.getByRole('button', { name: 'Chiama' })).toHaveCount(0)
    await page.getByRole('tab', { name: 'Listone', exact: true }).click()
    await expect(
      page.getByRole('button', { name: 'D', exact: true }),
    ).toHaveAttribute('aria-pressed', 'true')
    await expect(
      page.getByRole('button', { name: 'Seleziona Alessandro Fabbri' }),
    ).toBeDisabled()
    await expect(
      page.getByRole('button', { name: 'Seleziona Calciatore 2' }),
    ).toBeEnabled()
    state.teams[1]!.defenders = league.defenders
    state.version++
    notify()
    await page.getByRole('tab', { name: 'Gestisci asta' }).click()
    await expect(
      page.getByRole('button', { name: 'Vai al turno di Real Sbronzi' }),
    ).toBeDisabled()
    await expect(
      page.getByRole('region', { name: 'Turno corrente', exact: true }),
    ).toContainText('Dinamo Divano')
    expect(control.commands).toHaveLength(0)
    await page.screenshot({
      path: test.info().outputPath(`role-phase-${width}.png`),
    })
  })
}

for (const width of [320, 1440]) {
  test(`Bomba: offerta segreta, riconnessione, rivelazione e spareggio a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    const { state, control, notify } = await setupRoom(page, {
      waiting: true,
      organizer: true,
    })
    await page.getByRole('tab', { name: 'Listone', exact: true }).click()
    await page
      .getByRole('button', { name: 'Seleziona Alessandro Fabbri' })
      .click()
    await page.getByRole('button', { name: 'Sgancia la bomba' }).click()
    const arena = page.getByRole('region', { name: 'Asta Bomba', exact: true })
    await expect(arena).toBeVisible()
    await expect(page.getByRole('tablist')).toBeHidden()
    await expect(page.getByRole('button', { name: 'Chiama' })).toHaveCount(0)
    const offer = page.getByLabel('La tua offerta segreta')
    await expect(arena.getByText('La sfida sta per iniziare')).toBeVisible()
    await expect(offer).toHaveCount(0)
    await expect(arena.getByText('In attesa', { exact: true })).toHaveCount(4)
    await expect(page.locator('.app-header')).toBeHidden()
    await expect(page.locator('body')).toHaveCSS(
      'background-color',
      'rgb(26, 17, 37)',
    )
    await expect(arena).toHaveCSS('opacity', '1')
    await expect(arena.locator('.bomb-lobby-quote')).toHaveCSS('opacity', '1')
    await page.screenshot({
      path: test.info().outputPath(`bomb-lobby-${width}.png`),
      fullPage: true,
    })
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    const firstQuote = await arena.locator('.bomb-lobby-quote').textContent()
    state.currentBomb!.deadline = new Date(Date.now() + 51000).toISOString()
    state.version++
    notify()
    await expect(arena.locator('.bomb-lobby-quote')).not.toHaveText(firstQuote!)
    const previousQuote = await arena.locator('.bomb-lobby-quote').textContent()
    state.currentBomb!.deadline = new Date(Date.now() + 42000).toISOString()
    state.version++
    notify()
    await expect(arena.getByRole('progressbar')).toHaveAttribute(
      'value',
      /4[012]/,
    )
    await expect(arena.locator('.bomb-lobby-quote')).not.toHaveText(
      previousQuote!,
    )
    await page.reload()
    await expect(arena.getByText('La sfida sta per iniziare')).toBeVisible()
    await expect(offer).toHaveCount(0)
    expect(control.commands).toHaveLength(1)
    state.currentBomb!.deadline = new Date(Date.now() - 1000).toISOString()
    state.version++
    notify()
    await expect(arena.getByRole('progressbar')).toHaveAttribute('value', '0')
    await expect(offer).toHaveCount(0)
    await expect(
      arena.getByText('Ci siamo. Attendiamo l’apertura delle offerte.'),
    ).toBeVisible()
    state.currentBomb!.status = 'Collecting'
    state.currentBomb!.deadline = new Date(Date.now() + 60000).toISOString()
    state.version++
    notify()
    await expect(offer).toHaveAttribute('max', '476')
    await page.evaluate(() => window.dispatchEvent(new Event('offline')))
    await expect(offer).toBeDisabled()
    await page.evaluate(() => window.dispatchEvent(new Event('online')))
    await expect(offer).toBeEnabled()
    await offer.fill('35')
    await expect(arena).toHaveCSS('opacity', '1')
    await page.screenshot({
      path: test.info().outputPath(`bomb-offer-${width}.png`),
      fullPage: true,
    })
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true)
    control.uncertain = true
    await page.getByRole('button', { name: 'Conferma offerta' }).click()
    await expect(
      page.getByRole('button', { name: 'Verifica esito' }),
    ).toBeVisible()
    await page.getByRole('button', { name: 'Verifica esito' }).click()
    await expect(
      arena.getByText('Offerta confermata', { exact: true }),
    ).toBeVisible()
    expect(control.commands).toHaveLength(2)
    expect(control.commands[1]).toMatchObject({
      bombAuctionId: aid,
      round: 1,
      amount: 35,
    })
    state.currentBomb!.status = 'Revealing'
    state.currentBomb!.revealStartedAt = new Date().toISOString()
    state.currentBomb!.nextRevealAt = new Date(Date.now() + 3000).toISOString()
    state.version++
    notify()
    await expect(arena.getByText('Offerte chiuse. Ci siamo…')).toBeVisible()
    await expect(offer).toHaveCount(0)
    state.currentBomb!.revealedOffers = [{ teamId: teamId(1), amount: 12 }]
    state.version++
    notify()
    await expect(arena.getByText('12 crediti')).toBeVisible()
    await expect(arena.getByText('35 crediti')).toHaveCount(0)
    await page.screenshot({
      path: test.info().outputPath(`bomb-reveal-${width}.png`),
      fullPage: true,
    })
    state.currentBomb!.status = 'Collecting'
    state.currentBomb!.round = 2
    state.currentBomb!.minimumAmount = 35
    state.currentBomb!.ownAmount = null
    state.currentBomb!.revealedOffers = []
    state.currentBomb!.participants = [
      { teamId: teamId(0), hasSubmitted: false },
      { teamId: teamId(2), hasSubmitted: false },
    ]
    state.currentBomb!.deadline = new Date(Date.now() + 60000).toISOString()
    state.version++
    notify()
    await expect(
      arena.getByRole('heading', { name: 'Spareggio · Round 2' }),
    ).toBeVisible()
    await expect(offer).toHaveValue('')
    await expect(offer).toHaveAttribute('min', '35')
    await offer.fill('34')
    await expect(
      page.getByRole('button', { name: 'Conferma offerta' }),
    ).toBeDisabled()
    control.uncertain = false
    await page.getByRole('button', { name: 'Annulla Bomba' }).click()
    await expect(
      arena.getByText('Bomba annullata', { exact: true }),
    ).toBeVisible()
    await page.getByRole('button', { name: 'Torna alla sala' }).click()
    await expect(arena).toHaveCount(0)
    await expect(page.getByRole('tablist')).toBeVisible()
    await page.reload()
    await expect(page.getByRole('tablist')).toBeVisible()
    await expect(page.locator('.app-header')).toBeVisible()
    await expect(arena).toHaveCount(0)
    state.currentBomb = {
      ...state.currentBomb!,
      id: '00000000-0000-4000-8000-000000000099',
      status: 'Waiting',
      round: 1,
    }
    state.version++
    notify()
    await expect(arena).toBeVisible()
    await expect(page.getByRole('tablist')).toBeHidden()
    await expect(offer).toHaveCount(0)
    await page.getByRole('button', { name: 'Annulla Bomba' }).click()
    await expect(
      arena.getByText('Bomba annullata', { exact: true }),
    ).toBeVisible()
  })
}

test('Bomba: uno spettatore entra dalla tab Rose, segue il vincitore e non ripete la fanfara al reload', async ({
  page,
}) => {
  const { state, control, notify } = await setupRoom(page, { waiting: true })
  await page.getByRole('tab', { name: 'Rose', exact: true }).click()
  state.currentBomb = {
    id: aid,
    playerId,
    name: 'Alessandro Fabbri',
    role: 'A',
    clubName: 'Demo Aurora',
    photoUrl: null,
    clubLogoUrl: null,
    callerTeamId: teamId(1),
    status: 'Collecting',
    round: 2,
    minimumAmount: 35,
    deadline: new Date(Date.now() + 60000).toISOString(),
    revealStartedAt: null,
    nextRevealAt: null,
    participants: [
      { teamId: teamId(1), hasSubmitted: false },
      { teamId: teamId(2), hasSubmitted: false },
    ],
    revealedOffers: [],
    ownAmount: null,
    playerAuctionId: null,
    winningTeamId: null,
    winningAmount: null,
  }
  state.version++
  notify()
  const arena = page.getByRole('region', { name: 'Asta Bomba', exact: true })
  await expect(arena).toBeVisible()
  await expect(
    page.getByRole('tabpanel', { name: 'Rose', exact: true }),
  ).toBeHidden()
  await expect(page.getByLabel('La tua offerta segreta')).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Annulla Bomba' })).toHaveCount(
    0,
  )
  state.currentBomb.status = 'Revealing'
  state.currentBomb.revealedOffers = [
    { teamId: teamId(2), amount: 38 },
    { teamId: teamId(1), amount: 45 },
  ]
  state.version++
  notify()
  await expect(arena.getByText('45 crediti')).toBeVisible()
  await expect(
    page.getByRole('region', { name: 'Aggiudicazione', exact: true }),
  ).toHaveCount(0)
  state.currentBomb.status = 'Completed'
  state.currentBomb.winningTeamId = teamId(1)
  state.currentBomb.winningAmount = 45
  state.currentBomb.playerAuctionId = aid
  state.currentAuction = {
    id: aid,
    playerId,
    name: 'Alessandro Fabbri',
    role: 'A',
    clubName: 'Demo Aurora',
    callerTeamId: teamId(1),
    winningTeamId: teamId(1),
    currentAmount: 45,
    durationSeconds: 60,
    increments: [],
    deadline: new Date().toISOString(),
    status: 'Closed',
    startedAt: new Date().toISOString(),
    closedAt: new Date().toISOString(),
  }
  state.teams[1]!.budget -= 45
  state.teams[1]!.forwards++
  state.version++
  notify()
  const celebration = page.getByRole('region', {
    name: 'Aggiudicazione',
    exact: true,
  })
  await expect(celebration).toContainText('Real Sbronzi')
  await expect(page.locator('.auction-victory-confetti svg')).toHaveCount(1)
  await expect
    .poll(() =>
      page.evaluate(
        () =>
          (window as unknown as { auctionAudioPlays: number })
            .auctionAudioPlays,
      ),
    )
    .toBe(1)
  await page.getByRole('button', { name: 'Chiudi celebrazione' }).click()
  await expect(arena.getByText('Aggiudicato!', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Torna alla sala' }).click()
  await expect(
    page.getByRole('tabpanel', { name: 'Rose', exact: true }),
  ).toBeVisible()
  expect(control.commands).toHaveLength(0)
  await page.reload()
  await expect(
    page.getByRole('tab', { name: 'Live', exact: true }),
  ).toBeVisible()
  await expect(arena).toHaveCount(0)
  await expect(celebration).toHaveCount(0)
  expect(
    await page.evaluate(
      () =>
        (window as unknown as { auctionAudioPlays: number }).auctionAudioPlays,
    ),
  ).toBe(0)
})

for (const width of [320, 768, 1440]) {
  test(`rose a card: gruppi e navigazione responsive a ${width}px`, async ({
    page,
  }) => {
    await page.setViewportSize({ width, height: 1000 })
    await setupRoom(page, {
      waiting: true,
      teamCount: 10,
      rosterRoles: ['A', 'C', 'P', 'D', 'P'],
    })
    await page.getByRole('tab', { name: 'Rose', exact: true }).click()
    const panel = page.getByRole('tabpanel', { name: 'Rose', exact: true })
    await expect(panel.getByRole('article')).toHaveCount(10)
    const mine = panel.getByRole('article', {
      name: 'Rosa Atletico Spritz',
      exact: true,
    })
    await expect(mine.getByRole('heading', { level: 4 })).toHaveText([
      'Portieri',
      'Difensori',
      'Centrocampisti',
      'Attaccanti',
    ])
    await expect(mine).toContainText('Budget')
    await expect(mine).toContainText('Offerta max')
    await expect(
      panel.getByRole('article', { name: 'Rosa Real Sbronzi', exact: true }),
    ).toContainText('Il primo acquisto ti aspetta')
    const next = panel.getByRole('button', { name: 'Squadre successive' })
    if (width < 1200) {
      await expect(next).toBeVisible()
      await next.click()
      const board = panel.getByLabel('Scorri le squadre', { exact: true })
      await board.focus()
      await board.press('End')
      await expect(
        panel.getByRole('article', {
          name: 'Rosa AS Intomatici',
          exact: true,
        }),
      ).toBeInViewport({ ratio: 0.9 })
      await board.press('Home')
      await expect(mine).toBeInViewport({ ratio: 0.9 })
    } else {
      await expect(next).toBeHidden()
      const first = await mine.boundingBox()
      const last = await panel
        .getByRole('article', { name: 'Rosa AS Intomatici', exact: true })
        .boundingBox()
      const fifth = await panel.getByRole('article').nth(4).boundingBox()
      const sixth = await panel.getByRole('article').nth(5).boundingBox()
      expect(fifth!.y).toBe(first!.y)
      expect(sixth!.y).toBeGreaterThan(first!.y)
      expect(last!.y).toBe(sixth!.y)
    }
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth),
    ).toBeLessThanOrEqual(width)
    await page.screenshot({
      path: test.info().outputPath(`rose-${width}.png`),
      fullPage: true,
    })
  })
}

test('scroll infinito: errore, fine lista e acquisti realtime non perdono o duplicano calciatori', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 900 })
  const { control, state, notify } = await setupRoom(page, {
    waiting: true,
    catalogSize: 61,
  })
  let rejected = 0
  let fail = true
  await page.route('**/Catalog?**', async (route) => {
    if (
      fail &&
      new URL(route.request().url()).searchParams.get('page') === '2'
    ) {
      rejected++
      await route.fulfill({
        status: 503,
        contentType: 'application/json',
        body: JSON.stringify({
          isSuccess: false,
          data: null,
          errors: [{ code: 'test.error', message: 'Errore di prova' }],
        }),
      })
    } else await route.fallback()
  })
  await page.getByRole('tab', { name: 'Listone', exact: true }).click()
  const catalog = page.locator('.auction-catalog--expanded')
  await expect(catalog.locator('.catalog-row')).toHaveCount(30)
  await catalog.locator('.infinite-scroll-more').scrollIntoViewIfNeeded()
  await expect(catalog.getByRole('alert')).toBeVisible()
  expect(rejected).toBe(1)
  await expect(catalog.locator('.catalog-row')).toHaveCount(30)
  fail = false
  await catalog
    .getByRole('button', {
      name: 'Riprova: Carica altri calciatori',
      exact: true,
    })
    .click()
  await expect(catalog.locator('.catalog-row')).toHaveCount(60)
  await catalog.locator('.infinite-scroll-more').scrollIntoViewIfNeeded()
  await expect(catalog.locator('.catalog-row')).toHaveCount(61)
  await expect(catalog.locator('.infinite-scroll-more')).toHaveCount(0)
  expect(control.catalogRequests.every((pageNumber) => pageNumber <= 3)).toBe(
    true,
  )
  control.catalogRemoved.push(playerId, 'player-1')
  state.version++
  notify()
  await expect(catalog.locator('.catalog-row')).toHaveCount(59)
  await expect(
    catalog.getByText('Alessandro Fabbri', { exact: true }),
  ).toHaveCount(0)
  await expect(catalog.getByText('Calciatore 61', { exact: true })).toHaveCount(
    1,
  )
  const names = await catalog.locator('.catalog-row h3').allTextContents()
  expect(new Set(names).size).toBe(names.length)
})

test('ogni rosa carica scorrendo senza perdere acquisti e lo storico resta completo', async ({
  page,
}) => {
  await setupRoom(page, {
    waiting: true,
    rosterRoles: Array.from({ length: 101 }, () => 'P' as const),
  })
  await page.getByRole('tab', { name: 'Rose', exact: true }).click()
  const mine = page.getByRole('article', {
    name: 'Rosa Atletico Spritz',
    exact: true,
  })
  await mine.locator('.infinite-scroll-more').scrollIntoViewIfNeeded()
  await expect(mine.getByText('Calciatore 101', { exact: true })).toBeVisible()
  await expect(mine.getByText('Calciatore 1', { exact: true })).toHaveCount(1)
  await page.getByRole('tab', { name: 'Storico', exact: true }).click()
  await expect(
    page
      .getByRole('tabpanel', { name: 'Storico' })
      .getByText('Calciatore 1', { exact: true }),
  ).toBeVisible()
  const history = page.getByRole('tabpanel', { name: 'Storico' })
  await history.locator('.infinite-scroll-more').scrollIntoViewIfNeeded()
  await expect(history.locator('.catalog-row')).toHaveCount(101)
  await expect(history.getByText('Calciatore 1', { exact: true })).toHaveCount(
    1,
  )
  await page.getByRole('tab', { name: 'Rose', exact: true }).click()
  await expect(mine.getByText('Calciatore 101', { exact: true })).toBeVisible()
})

test('le rose passano da griglia a carosello al resize e raggiungono tutte le squadre', async ({
  page,
}) => {
  await page.setViewportSize({ width: 1440, height: 1000 })
  await setupRoom(page, { waiting: true, teamCount: 8 })
  await page.getByRole('tab', { name: 'Rose', exact: true }).click()
  const panel = page.getByRole('tabpanel', { name: 'Rose', exact: true })
  const next = panel.getByRole('button', { name: 'Squadre successive' })
  await expect(next).toBeHidden()
  await page.setViewportSize({ width: 390, height: 1000 })
  await expect(next).toBeEnabled()
  const board = panel.getByLabel('Scorri le squadre', { exact: true })
  await board.focus()
  await board.press('End')
  const last = panel.getByRole('article', {
    name: 'Rosa FC Mai una Gioia',
    exact: true,
  })
  await expect(last).toBeInViewport({ ratio: 0.9 })
  await page.setViewportSize({ width: 1440, height: 1000 })
  await expect(next).toBeHidden()
  const first = panel.getByRole('article', {
    name: 'Rosa Atletico Spritz',
    exact: true,
  })
  await expect(first).toBeInViewport()
  expect((await last.boundingBox())!.y).toBeGreaterThan(
    (await first.boundingBox())!.y,
  )
})

test('un errore caricando la rosa conserva gli acquisti e permette di riprovare', async ({
  page,
}) => {
  await setupRoom(page, {
    waiting: true,
    rosterRoles: Array.from({ length: 101 }, () => 'P' as const),
  })
  let reject = true
  await page.route('**/Roster?**', async (route) => {
    const params = new URL(route.request().url()).searchParams
    if (
      reject &&
      params.get('teamId') === teamId(0) &&
      params.get('page') === '2'
    ) {
      await route.fulfill({
        status: 400,
        contentType: 'application/problem+json',
        body: JSON.stringify({ title: 'Errore di prova', status: 400 }),
      })
    } else await route.fallback()
  })
  await page.getByRole('tab', { name: 'Rose', exact: true }).click()
  const mine = page.getByRole('article', {
    name: 'Rosa Atletico Spritz',
    exact: true,
  })
  await mine.locator('.infinite-scroll-more').scrollIntoViewIfNeeded()
  await expect(mine.getByRole('alert')).toContainText(
    'Non riusciamo a caricare altri risultati.',
  )
  await expect(mine.getByText('Calciatore 1', { exact: true })).toHaveCount(1)
  reject = false
  await mine
    .getByRole('button', {
      name: 'Riprova: Carica altri giocatori di Atletico Spritz',
    })
    .click()
  await expect(mine.getByText('Calciatore 101', { exact: true })).toBeVisible()
})
