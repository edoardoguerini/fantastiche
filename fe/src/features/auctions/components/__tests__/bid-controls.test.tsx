import { fireEvent, render, screen } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import type { TimedSession } from '../../types/auction.types'
import { BidControls } from '../bid-controls'

const team = {
  id: 'team',
  name: 'Squadra',
  budget: 100,
  goalkeepers: 0,
  defenders: 0,
  midfielders: 0,
  forwards: 0,
}
const rules = {
  budget: 100,
  goalkeepers: 1,
  defenders: 1,
  midfielders: 1,
  forwards: 1,
}
const session: TimedSession = {
  id: 'session',
  leagueId: 'league',
  leagueSeasonId: 'season',
  listVersionId: 'list',
  version: 1,
  status: 'Active',
  currentRole: 'P',
  currentTeamId: 'opponent',
  teamOrder: ['opponent', team.id],
  teams: [team],
  serverTime: '2026-09-14T20:00:00Z',
  receivedAt: 0,
  currentAuction: {
    id: 'auction',
    playerId: 'player',
    name: 'Portiere',
    role: 'P',
    clubName: 'Club',
    callerTeamId: 'opponent',
    winningTeamId: 'opponent',
    currentAmount: 5,
    durationSeconds: 30,
    increments: [5, 10],
    deadline: '2026-09-14T20:00:30Z',
    startedAt: '2026-09-14T20:00:00Z',
    status: 'Open',
    closedAt: null,
  },
}

it.each([
  [5, 10],
  [1, 5, 10],
])(
  'mostra un solo +1 anche nelle aste precedenti e invia il totale corretto (%j)',
  (...increments) => {
    const onBid = vi.fn()
    render(
      <BidControls
        session={{
          ...session,
          currentAuction: { ...session.currentAuction!, increments },
        }}
        team={team}
        rules={rules}
        connected
        blocked={false}
        seconds={20}
        onBid={onBid}
      />,
    )
    const one = screen.getByRole('button', { name: 'Offri 6 crediti, più 1' })
    fireEvent.click(one)
    expect(onBid).toHaveBeenCalledExactlyOnceWith(6)
    expect(
      screen
        .getAllByRole('button')
        .slice(0, 3)
        .map((button) => button.textContent),
    ).toEqual(['+1', '+5', '+10'])
  },
)
