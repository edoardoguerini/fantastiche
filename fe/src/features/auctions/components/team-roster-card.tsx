import { useInfiniteQuery } from '@tanstack/react-query'
import { Icon } from '@/components/common/icon'
import { Button } from '@/components/primitives/button'
import {
  roles,
  type AuctionTeam,
  type RosterRules,
} from '../types/auction.types'
import { rosterQueryOptions, loadedPlayers } from '../actions/auction.queries'
import { emptySlots, maxOffer } from '../validations/auction-rules'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'
import { InfiniteScrollMore } from './infinite-scroll-more'

export function TeamRosterCard({
  userId,
  sessionId,
  team,
  index,
  mine,
  current,
  rules,
  onSelect,
  expanded = false,
}: {
  userId: string
  sessionId: string
  team: AuctionTeam
  index: number
  mine: boolean
  current: boolean
  rules: RosterRules
  expanded?: boolean
  onSelect: (id: string) => void
}) {
  const roster = useInfiniteQuery(
    rosterQueryOptions(userId, sessionId, team.id),
  )
  const entries = loadedPlayers(roster.data?.pages)
  const header = (
    <>
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
    </>
  )
  return (
    <article
      aria-label={expanded ? `Rosa ${team.name}` : undefined}
      tabIndex={expanded ? -1 : undefined}
      className={`team-column ${mine ? 'team-column--mine' : ''} ${current ? 'team-column--current' : ''}`}
    >
      {expanded ? (
        <header>{header}</header>
      ) : (
        <button
          className="team-open"
          type="button"
          onClick={() => onSelect(team.id)}
          aria-label={`Apri rosa ${team.name}`}
        >
          {header}
        </button>
      )}
      <div
        className="team-purchases"
        aria-busy={roster.isFetching}
        tabIndex={0}
        aria-label={`Acquisti ${team.name}`}
      >
        {roster.isPending ? (
          <p className="team-purchases-empty">Caricamento rosa…</p>
        ) : roster.isError && !roster.data ? (
          <div className="team-roster-error">
            <p>Non riusciamo a caricare la rosa.</p>
            <Button variant="ghost" onClick={() => void roster.refetch()}>
              Ricarica la rosa
            </Button>
          </div>
        ) : entries.length ? (
          roles.map((role) => {
            const players = entries.filter((player) => player.role === role.id)
            if (!players.length) return null
            return (
              <div className="team-purchase-group" key={role.id}>
                <h4>{role.label}</h4>
                <ul aria-label={role.label}>
                  {players.map((player) => (
                    <li key={player.playerId}>
                      <PlayerPhoto url={player.photoUrl} role={player.role} />
                      <div className="team-purchase-details">
                        <span>{player.name}</span>
                        <span className="team-player-meta">
                          <span className={`catalog-role role-${player.role}`}>
                            {player.role}
                          </span>
                          {expanded && (
                            <ClubLabel
                              name={player.clubName}
                              logoUrl={player.clubLogoUrl}
                            />
                          )}
                        </span>
                      </div>
                      <strong
                        aria-label={`${player.price} ${player.price === 1 ? 'credito' : 'crediti'}`}
                      >
                        {player.price}
                      </strong>
                    </li>
                  ))}
                </ul>
              </div>
            )
          })
        ) : (
          <p className="team-purchases-empty">
            <Icon name="shirt" variant="jelly" />
            <span>Il primo acquisto ti aspetta</span>
          </p>
        )}
        <InfiniteScrollMore
          hasMore={roster.hasNextPage}
          fetching={roster.isFetching}
          error={roster.isError && !!roster.data}
          label={`Carica altri giocatori di ${team.name}`}
          onLoad={() => {
            if (roster.isFetching) return
            if (roster.isRefetchError) void roster.refetch()
            else void roster.fetchNextPage({ cancelRefetch: false })
          }}
        />
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
