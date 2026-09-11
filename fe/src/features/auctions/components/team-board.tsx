import { useEffect, useId, useState } from 'react'
import useEmblaCarousel from 'embla-carousel-react'
import { Icon } from '@/components/common/icon'
import type { AuctionTeam, RosterRules } from '../types/auction.types'
import { TeamRosterCard } from './team-roster-card'

const carouselOptions = {
  loop: true,
  align: () => 44,
  breakpoints: { '(prefers-reduced-motion: reduce)': { duration: 0 } },
}

const rosterCarouselOptions = {
  loop: false,
  align: 'start' as const,
  breakpoints: {
    '(prefers-reduced-motion: reduce)': { duration: 0 },
    '(min-width: 1200px)': { active: false },
  },
}

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
  const [boardRef, carousel] = useEmblaCarousel(
    expanded ? rosterCarouselOptions : carouselOptions,
  )
  const boardId = useId()
  const [edges, setEdges] = useState({ start: true, end: true })

  const selectedIndex = teams.findIndex((team) => team.id === selectedTeamId)
  useEffect(() => {
    if (!carousel || !expanded || !active) return
    carousel.reInit()
    if (selectedIndex < 0) return
    carousel.scrollTo(selectedIndex, true)
    const card = carousel.slideNodes()[selectedIndex]
    card?.focus({ preventScroll: true })
    if (window.matchMedia('(min-width: 1200px)').matches) {
      card?.scrollIntoView({ block: 'nearest', inline: 'nearest' })
    }
  }, [carousel, expanded, active, selectedIndex])

  useEffect(() => {
    if (!carousel) return
    const updateEdges = () => {
      const start = !carousel.canScrollPrev()
      const end = !carousel.canScrollNext()
      setEdges((previous) =>
        previous.start === start && previous.end === end
          ? previous
          : { start, end },
      )
    }
    carousel.on('select', updateEdges).on('reInit', updateEdges)
    updateEdges()

    // Il gesto orizzontale del trackpad muove le card; quello verticale resta alla pagina.
    let wheelDistance = 0
    let lastWheelTime = 0
    let lastStepTime = 0
    const onWheel = (event: WheelEvent) => {
      if (expanded && window.matchMedia('(min-width: 1200px)').matches) return
      if (event.ctrlKey || Math.abs(event.deltaX) <= Math.abs(event.deltaY))
        return
      if (!carousel.canScrollPrev() && !carousel.canScrollNext()) return
      event.preventDefault()
      const now = performance.now()
      if (now - lastWheelTime > 180) wheelDistance = 0
      lastWheelTime = now
      wheelDistance += event.deltaX
      if (Math.abs(wheelDistance) < 30 || now - lastStepTime < 220) return
      if (wheelDistance > 0) carousel.scrollNext()
      else carousel.scrollPrev()
      wheelDistance = 0
      lastStepTime = now
    }
    const viewport = carousel.rootNode()
    viewport.addEventListener('wheel', onWheel, { passive: false })
    return () => {
      carousel.off('select', updateEdges).off('reInit', updateEdges)
      viewport.removeEventListener('wheel', onWheel)
    }
  }, [carousel, expanded])

  function scrollTeams(direction: -1 | 1) {
    if (direction < 0) carousel?.scrollPrev()
    else carousel?.scrollNext()
  }

  return (
    <section
      className={`auction-teams ${expanded ? 'auction-teams--rosters' : ''}`}
      aria-label={expanded ? 'Rose delle squadre' : 'Tabellone delle squadre'}
      aria-roledescription={expanded ? undefined : 'carosello'}
    >
      <div className="auction-teams-heading">
        <h2>{expanded ? `${teams.length} squadre` : 'Le squadre'}</h2>
        <div className="auction-teams-navigation">
          {!expanded && <span>{teams.length} partecipanti</span>}
          <div className="auction-carousel-controls">
            <button
              type="button"
              aria-label="Squadre precedenti"
              aria-controls={boardId}
              disabled={edges.start}
              onClick={() => scrollTeams(-1)}
            >
              <Icon name="chevron-left" />
            </button>
            <button
              type="button"
              aria-label="Squadre successive"
              aria-controls={boardId}
              disabled={edges.end}
              onClick={() => scrollTeams(1)}
            >
              <Icon name="chevron-right" />
            </button>
          </div>
        </div>
      </div>
      <div
        ref={boardRef}
        id={boardId}
        className="auction-board"
        tabIndex={0}
        aria-label="Scorri le squadre"
        onKeyDown={(event) => {
          if (event.target !== event.currentTarget) return
          if (expanded && window.matchMedia('(min-width: 1200px)').matches)
            return
          if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
            event.preventDefault()
            scrollTeams(event.key === 'ArrowLeft' ? -1 : 1)
          } else if (event.key === 'Home' || event.key === 'End') {
            event.preventDefault()
            carousel?.scrollTo(event.key === 'Home' ? 0 : teams.length - 1)
          }
        }}
      >
        <div className="auction-board-track">
          {teams.map((team, index) => (
            <TeamRosterCard
              key={team.id}
              userId={userId}
              sessionId={sessionId}
              team={team}
              index={index}
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
