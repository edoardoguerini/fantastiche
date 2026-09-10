import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, afterEach, expect, it, vi } from 'vitest'
import { BombAuction } from '../bomb-auction'
import type { BombAuctionView, TimedSession } from '../../types/auction.types'

beforeEach(() => vi.stubGlobal('scrollTo', vi.fn()))
afterEach(() => vi.unstubAllGlobals())

const bomb: BombAuctionView = {
  id: 'bomb',
  playerId: 'player',
  name: 'Aboukhlal',
  role: 'A',
  clubName: 'Torino',
  photoUrl: null,
  clubLogoUrl: null,
  callerTeamId: 'team',
  status: 'Collecting',
  round: 1,
  minimumAmount: 1,
  deadline: '2026-09-10T17:01:00Z',
  revealStartedAt: null,
  nextRevealAt: null,
  participants: [
    { teamId: 'team', hasSubmitted: false },
    { teamId: 'other', hasSubmitted: true },
  ],
  revealedOffers: [],
  ownAmount: null,
  playerAuctionId: null,
  winningTeamId: null,
  winningAmount: null,
}
const session: TimedSession = {
  id: 'session',
  leagueId: 'league',
  leagueSeasonId: 'season',
  listVersionId: 'list',
  version: 1,
  status: 'Active',
  currentRole: 'A',
  currentTeamId: 'team',
  teamOrder: ['team', 'other'],
  serverTime: '2026-09-10T17:00:00Z',
  receivedAt: performance.now(),
  currentAuction: null,
  currentBomb: bomb,
  teams: [
    {
      id: 'team',
      name: 'Real Sbronzi',
      budget: 50,
      goalkeepers: 0,
      defenders: 0,
      midfielders: 0,
      forwards: 0,
    },
    {
      id: 'other',
      name: 'Atletico Spritz',
      budget: 50,
      goalkeepers: 0,
      defenders: 0,
      midfielders: 0,
      forwards: 0,
    },
  ],
}
const props = {
  bomb,
  session,
  myTeamId: 'team',
  rules: {
    budget: 500,
    goalkeepers: 3,
    defenders: 8,
    midfielders: 8,
    forwards: 6,
  },
  connected: true,
  blocked: false,
  canManage: false,
  onBid: vi.fn(async (_amount: number) => {}),
  onCancel: vi.fn(async () => {}),
  onDismiss: vi.fn(),
}
it('riserva gli altri posti della rosa e conferma solo importi interi validi', async () => {
  render(<BombAuction {...props} />)
  const input = screen.getByLabelText('La tua offerta segreta')
  const submit = screen.getByRole('button', { name: 'Conferma offerta' })
  expect(input).toHaveAttribute('max', '26')
  fireEvent.change(input, { target: { value: '27' } })
  expect(submit).toBeDisabled()
  fireEvent.change(input, { target: { value: '2.5' } })
  expect(submit).toBeDisabled()
  fireEvent.change(input, { target: { value: '26' } })
  fireEvent.click(submit)
  await waitFor(() => expect(props.onBid).toHaveBeenCalledWith(26))
  expect(screen.queryByRole('button', { name: 'Passo' })).toBeNull()
})
it('mantiene segreti gli importi altrui e rende immutabile la propria conferma', () => {
  render(
    <BombAuction
      {...props}
      bomb={{
        ...bomb,
        ownAmount: 19,
        participants: bomb.participants.map((p) => ({
          ...p,
          hasSubmitted: true,
        })),
      }}
    />,
  )
  expect(screen.queryByLabelText('La tua offerta segreta')).toBeNull()
  expect(screen.getByText('19 crediti')).toBeVisible()
  expect(screen.getByText('Offerta confermata')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Conferma offerta' })).toBeNull()
})
it('blocca offline, a scadenza e per squadre escluse dallo spareggio', () => {
  const view = render(<BombAuction {...props} connected={false} />)
  expect(screen.getByLabelText('La tua offerta segreta')).toBeDisabled()
  view.rerender(
    <BombAuction {...props} bomb={{ ...bomb, deadline: session.serverTime }} />,
  )
  expect(screen.getByLabelText('La tua offerta segreta')).toBeDisabled()
  view.rerender(
    <BombAuction
      {...props}
      bomb={{
        ...bomb,
        round: 2,
        minimumAmount: 20,
        participants: [bomb.participants[1]!],
      }}
    />,
  )
  expect(
    screen.getByRole('heading', { name: 'Spareggio · Round 2' }),
  ).toBeVisible()
  expect(screen.queryByLabelText('La tua offerta segreta')).toBeNull()
})
it('mostra soltanto le offerte rivelate dal server e non deduce il vincitore', () => {
  const view = render(
    <BombAuction
      {...props}
      bomb={{
        ...bomb,
        status: 'Revealing',
        revealStartedAt: session.serverTime,
        nextRevealAt: bomb.deadline,
      }}
    />,
  )
  expect(screen.queryByLabelText('La tua offerta segreta')).toBeNull()
  expect(screen.queryByText('12 crediti')).toBeNull()
  view.rerender(
    <BombAuction
      {...props}
      bomb={{
        ...bomb,
        status: 'Revealing',
        revealedOffers: [{ teamId: 'other', amount: 12 }],
      }}
    />,
  )
  expect(screen.getByText('12 crediti')).toBeVisible()
  expect(screen.queryByText('Aggiudicato!')).toBeNull()
  expect(screen.queryByRole('button', { name: 'Torna alla sala' })).toBeNull()
})

it('la sala di attesa non apre offerte a zero e lascia annullare solo il gestore', () => {
  const waiting = { ...bomb, status: 'Waiting' as const }
  const view = render(<BombAuction {...props} bomb={waiting} canManage />)
  expect(screen.getByText('La sfida sta per iniziare')).toBeVisible()
  expect(screen.queryByLabelText('La tua offerta segreta')).toBeNull()
  expect(screen.getAllByText('In attesa')).toHaveLength(2)
  expect(screen.getByRole('button', { name: 'Annulla Bomba' })).toBeEnabled()
  view.rerender(
    <BombAuction
      {...props}
      bomb={{ ...waiting, deadline: session.serverTime }}
      canManage
    />,
  )
  expect(screen.getByLabelText('Tempo prima della sfida')).toHaveAttribute(
    'value',
    '0',
  )
  expect(screen.queryByLabelText('La tua offerta segreta')).toBeNull()
  expect(screen.queryByRole('button', { name: 'Torna alla sala' })).toBeNull()
  fireEvent.click(screen.getByRole('button', { name: 'Annulla Bomba' }))
  expect(props.onCancel).toHaveBeenCalled()
  view.rerender(<BombAuction {...props} bomb={bomb} />)
  expect(screen.getByLabelText('La tua offerta segreta')).toBeEnabled()
  expect(screen.queryByRole('blockquote')).toBeNull()
})

it('fa scorrere automaticamente le citazioni con il countdown condiviso', () => {
  const waiting = { ...bomb, status: 'Waiting' as const }
  const view = render(<BombAuction {...props} bomb={waiting} />)
  const quote = () => document.querySelector('.bomb-lobby-quote')!.textContent
  const initial = quote()
  const later = {
    ...session,
    serverTime: '2026-09-10T17:00:09Z',
    receivedAt: performance.now(),
  }
  view.rerender(<BombAuction {...props} bomb={waiting} session={later} />)
  expect(quote()).not.toBe(initial)
  const previous = quote()
  view.rerender(
    <BombAuction
      {...props}
      bomb={waiting}
      session={{ ...later, serverTime: '2026-09-10T17:00:18Z' }}
    />,
  )
  expect(quote()).not.toBe(previous)
  expect(screen.getByLabelText('Tempo prima della sfida')).toHaveAttribute(
    'value',
    '42',
  )
})
