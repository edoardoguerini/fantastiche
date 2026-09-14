import { useEffect, useRef, useSyncExternalStore } from 'react'
import type { AuctionTeam, RosterRules } from '../types/auction.types'
import { TeamRosterCard } from './team-roster-card'

const gridMedia = '(min-width: 1024px)'
function subscribeLayout(onChange: () => void) {
  const media = window.matchMedia(gridMedia)
  media.addEventListener('change', onChange)
  return () => media.removeEventListener('change', onChange)
}
const gridSnapshot = () => window.matchMedia(gridMedia).matches
const serverGridSnapshot = () => false

export function TeamBoard({
  userId,
  sessionId,
  teams,
  myTeamId,
  currentTeamId,
  rules,
  onSelect,
  expanded = false,
  active = true,
  selectedTeamId,
}: {
  userId: string
  sessionId: string
  teams: AuctionTeam[]
  myTeamId: string | null
  currentTeamId: string | null
  rules: RosterRules
  onSelect: (teamId: string) => void
  expanded?: boolean
  active?: boolean
  selectedTeamId?: string
}) {
  const grid = useSyncExternalStore(
    subscribeLayout,
    gridSnapshot,
    serverGridSnapshot,
  )
  const boardRef = useRef<HTMLDivElement>(null)
  const selectedIndex = teams.findIndex((team) => team.id === selectedTeamId)
  useEffect(() => {
    if (!expanded || !active || selectedIndex < 0) return
    const card = boardRef.current?.firstElementChild?.children[
      selectedIndex
    ] as HTMLElement | undefined
    card?.focus({ preventScroll: true })
    card?.scrollIntoView({
      block: 'nearest',
      inline: 'nearest',
      behavior: 'instant',
    })
  }, [expanded, active, selectedIndex, grid])

  return (
    <section
      className={`auction-teams ${expanded ? 'auction-teams--rosters' : 'auction-teams--live'}`}
      aria-label={expanded ? 'Rose delle squadre' : 'Tabellone delle squadre'}
    >
      <div
        ref={boardRef}
        className="auction-board"
        tabIndex={grid ? -1 : 0}
        aria-label={grid ? 'Squadre della lega' : 'Scorri le squadre'}
        onKeyDown={(event) => {
          if (event.target !== event.currentTarget) return
          if (grid) return
          if (event.key === 'Home' || event.key === 'End') {
            event.preventDefault()
            event.currentTarget.scrollTo({
              left: event.key === 'Home' ? 0 : event.currentTarget.scrollWidth,
              behavior: 'instant',
            })
          }
        }}
      >
        <div className="auction-board-track">
          {teams.map((team) => (
            <TeamRosterCard
              key={team.id}
              userId={userId}
              sessionId={sessionId}
              team={team}
              mine={team.id === myTeamId}
              current={team.id === currentTeamId}
              rules={rules}
              expanded={expanded}
              onSelect={onSelect}
            />
          ))}
        </div>
      </div>
    </section>
  )
}
