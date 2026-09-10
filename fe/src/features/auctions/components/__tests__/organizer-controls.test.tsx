import { fireEvent, render, screen } from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import { OrganizerControls } from '../organizer-controls'
import type { AuctionSession } from '../../types/auction.types'

const teams = ['Atletico Spritz', 'Real Sbronzi', 'Bayern Leverdure'].map(
  (name, i) => ({
    id: `team-${i}`,
    name,
    budget: 500,
    goalkeepers: 0,
    defenders: 0,
    midfielders: 0,
    forwards: 0,
  }),
)
const session: AuctionSession = {
  id: 's',
  leagueId: 'l',
  leagueSeasonId: 'ls',
  listVersionId: 'v',
  status: 'Active',
  version: 1,
  currentRole: 'P',
  currentTeamId: 'team-1',
  teamOrder: ['team-2', 'team-1', 'team-0'],
  teams,
  currentAuction: null,
  serverTime: '2026-09-10T19:00:00Z',
}
const rules = {
  budget: 500,
  goalkeepers: 3,
  defenders: 8,
  midfielders: 8,
  forwards: 6,
}

it('legge l’ordine autorevole, permette il salto diretto e salva solo il riordino richiesto', () => {
  const control = vi.fn(async () => {})
  render(
    <OrganizerControls
      session={session}
      disabled={false}
      onControl={control}
      myTeamId="team-0"
      rules={rules}
    />,
  )
  const rows = screen.getAllByRole('listitem')
  expect(rows[0]).toHaveTextContent('Bayern Leverdure')
  fireEvent.click(
    screen.getByRole('button', { name: 'Vai al turno di Atletico Spritz' }),
  )
  expect(control).toHaveBeenLastCalledWith('GoToTurn', undefined, 'team-0')
  fireEvent.click(
    screen.getByRole('button', { name: 'Sposta su Atletico Spritz' }),
  )
  expect(control).toHaveBeenCalledTimes(1)
  fireEvent.click(screen.getByRole('button', { name: 'Salva ordine' }))
  expect(control).toHaveBeenLastCalledWith('Reorder', [
    'team-2',
    'team-0',
    'team-1',
  ])
})

it('blocca i comandi durante una chiamata e conserva il riepilogo a sessione conclusa', () => {
  const control = vi.fn(async () => {})
  const view = render(
    <OrganizerControls
      session={{ ...session, status: 'Completed' }}
      disabled={false}
      onControl={control}
      myTeamId="team-0"
      rules={rules}
    />,
  )
  expect(screen.getByText('Sessione conclusa')).toBeVisible()
  expect(screen.getByRole('button', { name: 'Salta turno' })).toBeDisabled()
  expect(screen.getByRole('heading', { name: 'Ordine chiamate' })).toBeVisible()
  view.rerender(
    <OrganizerControls
      session={session}
      disabled
      onControl={control}
      myTeamId="team-0"
      rules={rules}
    />,
  )
  expect(
    screen.getByRole('button', { name: 'Vai al turno di Atletico Spritz' }),
  ).toBeDisabled()
})
