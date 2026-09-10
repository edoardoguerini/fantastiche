import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { StrictMode } from 'react'
import { AuctionVictory } from '../auction-victory'
import type { AuctionSession } from '../../types/auction.types'

const play = vi.fn(() => Promise.resolve())
const pause = vi.fn()
const session: AuctionSession = {
  id: 'session',
  leagueId: 'league',
  leagueSeasonId: 'season',
  listVersionId: 'list',
  version: 1,
  status: 'Active',
  currentRole: 'P',
  currentTeamId: 'team',
  teamOrder: ['team'],
  serverTime: '2026-09-10T17:00:00Z',
  teams: [
    {
      id: 'team',
      name: 'Real Sbronzi',
      budget: 480,
      goalkeepers: 0,
      defenders: 0,
      midfielders: 0,
      forwards: 1,
    },
  ],
  currentAuction: {
    id: 'auction',
    playerId: 'player',
    name: 'Aboukhlal',
    role: 'A',
    clubName: 'Torino',
    callerTeamId: 'team',
    winningTeamId: 'team',
    currentAmount: 20,
    durationSeconds: 30,
    increments: [1, 5],
    deadline: '2026-09-10T17:00:00Z',
    startedAt: '2026-09-10T16:59:30Z',
    status: 'Open',
    closedAt: null,
  },
}
const closed: AuctionSession = {
  ...session,
  version: 2,
  currentAuction: {
    ...session.currentAuction!,
    status: 'Closed',
    closedAt: session.serverTime,
  },
}

beforeEach(() => {
  play.mockReset().mockResolvedValue(undefined)
  pause.mockReset()
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: true })),
  )
  vi.stubGlobal(
    'Audio',
    class {
      play = play
      pause = pause
      currentTime = 0
    },
  )
})
afterEach(() => vi.unstubAllGlobals())

it('festeggia solo una chiusura confermata, una sola volta, anche per chi non ha vinto', async () => {
  const view = render(<AuctionVictory session={session} myTeamId="other" />, {
    wrapper: StrictMode,
  })
  expect(play).not.toHaveBeenCalled()
  expect(screen.queryByRole('status')).toBeNull()
  view.rerender(<AuctionVictory session={closed} myTeamId="other" />)
  expect(screen.getByRole('status')).toHaveTextContent('Real Sbronzi')
  expect(screen.getByRole('status')).toHaveTextContent('Aboukhlal')
  await waitFor(() => expect(play).toHaveBeenCalledTimes(1))
  view.rerender(
    <AuctionVictory session={{ ...closed, version: 3 }} myTeamId="other" />,
  )
  expect(play).toHaveBeenCalledTimes(1)
  fireEvent.keyDown(window, { key: 'Escape' })
  expect(screen.queryByRole('status')).toBeNull()
  expect(pause).toHaveBeenCalled()
  view.rerender(
    <AuctionVictory session={{ ...closed, version: 4 }} myTeamId="other" />,
  )
  expect(screen.queryByRole('status')).toBeNull()
})

it('non celebra vecchi acquisti entrando nella sala', () => {
  render(<AuctionVictory session={closed} myTeamId="team" />)
  expect(screen.queryByRole('status')).toBeNull()
  expect(play).not.toHaveBeenCalled()
})

it('rispetta il movimento ridotto e permette la fanfara quando autoplay è bloccato', async () => {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: true })),
  )
  play.mockRejectedValueOnce(new DOMException('Blocked', 'NotAllowedError'))
  const view = render(<AuctionVictory session={session} myTeamId="team" />)
  view.rerender(<AuctionVictory session={closed} myTeamId="team" />)
  expect(screen.getByRole('status')).toHaveTextContent('È tuo!')
  fireEvent.click(
    await screen.findByRole('button', { name: 'Riproduci fanfara' }),
  )
  await waitFor(() => expect(play).toHaveBeenCalledTimes(2))
  view.rerender(
    <AuctionVictory
      session={{
        ...session,
        version: 3,
        currentAuction: { ...session.currentAuction!, id: 'next' },
      }}
      myTeamId="team"
    />,
  )
  expect(screen.queryByRole('status')).toBeNull()
  expect(pause).toHaveBeenCalled()
})

it.each(['Waiting', 'Revealing'] as const)(
  'celebra la chiusura della Bomba osservata da %s, senza ripeterla al reload',
  async (status) => {
    const bomb = {
      id: 'bomb',
      playerId: 'player',
      name: 'Aboukhlal',
      role: 'A' as const,
      clubName: 'Torino',
      photoUrl: null,
      clubLogoUrl: null,
      callerTeamId: 'team',
      status,
      round: 1,
      minimumAmount: 1,
      deadline: session.serverTime,
      revealStartedAt: session.serverTime,
      nextRevealAt: session.serverTime,
      participants: [{ teamId: 'team', hasSubmitted: true }],
      revealedOffers: [],
      ownAmount: null,
      playerAuctionId: null,
      winningTeamId: null,
      winningAmount: null,
    }
    const collecting: AuctionSession = {
      ...session,
      currentAuction: null,
      currentBomb: bomb,
    }
    const completed: AuctionSession = {
      ...closed,
      currentBomb: {
        ...bomb,
        status: 'Completed',
        playerAuctionId: closed.currentAuction!.id,
        winningTeamId: 'team',
        winningAmount: 20,
      },
    }
    const view = render(
      <AuctionVictory session={collecting} myTeamId="other" />,
    )
    expect(play).not.toHaveBeenCalled()
    view.rerender(<AuctionVictory session={completed} myTeamId="other" />)
    expect(screen.getByRole('status')).toHaveTextContent('Aggiudicato!')
    await waitFor(() => expect(play).toHaveBeenCalledTimes(1))
    view.unmount()
    render(<AuctionVictory session={completed} myTeamId="other" />)
    expect(screen.queryByRole('status')).toBeNull()
    expect(play).toHaveBeenCalledTimes(1)
  },
)
