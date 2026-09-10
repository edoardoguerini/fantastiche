import { useEffect, useId, useState } from 'react'
import useEmblaCarousel from 'embla-carousel-react'
import { useQuery } from '@tanstack/react-query'
import { Icon } from '@/components/common/icon'
import {
  roles,
  type AuctionTeam,
  type RosterRules,
} from '../types/auction.types'
import { rosterQueryOptions } from '../actions/auction.queries'
import { emptySlots, maxOffer } from '../validations/auction-rules'
import { PlayerPhoto } from './player-photo'

const carouselOptions = {
  loop: true,
  align: () => 44,
  breakpoints: { '(prefers-reduced-motion: reduce)': { duration: 0 } },
}

export function TeamBoard({
  userId,
  sessionId,
  teams,
  myTeamId,
  currentTeamId,
  rules,
  onSelect,
}: {
  userId: string
  sessionId: string
  teams: AuctionTeam[]
  myTeamId: string | null
  currentTeamId: string | null
  rules: RosterRules
  onSelect: (teamId: string) => void
}) {
  const [boardRef, carousel] = useEmblaCarousel(carouselOptions)
  const boardId = useId()
  const [edges, setEdges] = useState({ start: true, end: true })

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
  }, [carousel])

  function scrollTeams(direction: -1 | 1) {
    if (direction < 0) carousel?.scrollPrev()
    else carousel?.scrollNext()
  }

  return (
    <section
      className="auction-teams"
      aria-label="Tabellone delle squadre"
      aria-roledescription="carosello"
    >
      <div className="auction-teams-heading">
        <h2>Le squadre</h2>
        <div className="auction-teams-navigation">
          <span>{teams.length} partecipanti</span>
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
            <TeamColumn
              key={team.id}
              userId={userId}
              sessionId={sessionId}
              team={team}
              index={index}
              mine={team.id === myTeamId}
              current={team.id === currentTeamId}
              rules={rules}
              onSelect={onSelect}
            />
          ))}
        </div>
      </div>
    </section>
  )
}
function TeamColumn({
  userId,
  sessionId,
  team,
  index,
  mine,
  current,
  rules,
  onSelect,
}: {
  userId: string
  sessionId: string
  team: AuctionTeam
  index: number
  mine: boolean
  current: boolean
  rules: RosterRules
  onSelect: (id: string) => void
}) {
  const roster = useQuery(rosterQueryOptions(userId, sessionId, team.id))
  return (
    <article
      className={`team-column ${mine ? 'team-column--mine' : ''} ${current ? 'team-column--current' : ''}`}
    >
      <button
        className="team-open"
        type="button"
        onClick={() => onSelect(team.id)}
        aria-label={`Apri rosa ${team.name}`}
      >
        <div className="team-column-top">
          <span className={`team-turn ${current ? 'team-turn--active' : ''}`}>
            {current ? 'Di turno' : String(index + 1).padStart(2, '0')}
          </span>
          {mine && <span className="team-mine">Tu</span>}
        </div>
        <div className="team-identity">
          <span className="team-monogram" aria-hidden="true">
            {team.name
              .split(' ')
              .slice(0, 2)
              .map((part) => part[0])
              .join('')}
          </span>
          <h3>{team.name}</h3>
        </div>
      </button>
      <div
        className="team-purchases"
        tabIndex={0}
        aria-label={`Acquisti ${team.name}`}
      >
        {roster.isPending ? (
          <p className="team-purchases-empty">Caricamento rosa…</p>
        ) : roster.isError ? (
          <button type="button" onClick={() => void roster.refetch()}>
            Ricarica la rosa
          </button>
        ) : roster.data.items.length ? (
          <ul>
            {roster.data.items.map((player) => (
              <li key={player.playerId}>
                <PlayerPhoto url={player.photoUrl} role={player.role} />
                <div className="team-purchase-details">
                  <span>{player.name}</span>
                  <span className={`catalog-role role-${player.role}`}>
                    {player.role}
                  </span>
                </div>
                <strong>{player.price}</strong>
              </li>
            ))}
          </ul>
        ) : (
          <p className="team-purchases-empty">
            <Icon name="shirt" variant="jelly" />
            <span>Il primo acquisto ti aspetta</span>
          </p>
        )}
        {roster.data && roster.data.total > roster.data.items.length && (
          <button type="button" onClick={() => onSelect(team.id)}>
            Apri la rosa completa
          </button>
        )}
      </div>
      <div className="team-capacity">
        {roles.map((role) => (
          <span key={role.id} title={role.label}>
            <b className={`role-${role.id}`}>{role.id}</b>
            {team[role.field]}
            <small>/{rules[role.field]}</small>
          </span>
        ))}
      </div>
      <div className="team-finances">
        <p>
          <span>Budget</span>
          <strong>{team.budget}</strong>
        </p>
        <p>
          <span>Offerta max</span>
          <strong>{maxOffer(team.budget, emptySlots(team, rules))}</strong>
        </p>
      </div>
    </article>
  )
}
