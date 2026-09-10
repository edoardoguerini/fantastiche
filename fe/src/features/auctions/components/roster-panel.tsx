import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Button } from '@/components/primitives/button'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { rosterQueryOptions } from '../actions/auction.queries'
import {
  roles,
  type AuctionTeam,
  type RosterRules,
} from '../types/auction.types'
import { emptySlots, maxOffer } from '../validations/auction-rules'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'

export function RosterPanel({
  userId,
  sessionId,
  teamId,
  teams,
  rules,
  history = false,
  onTeamChange,
}: {
  userId: string
  sessionId: string
  teamId: string
  teams: AuctionTeam[]
  rules?: RosterRules
  history?: boolean
  onTeamChange: (id: string) => void
}) {
  const [page, setPage] = useState(1)
  const team = teams.find((value) => value.id === teamId)
  const result = useQuery(
    rosterQueryOptions(userId, sessionId, history ? '' : teamId, page),
  )
  return (
    <div className="auction-roster-panel">
      {!history && (
        <div className="roster-select">
          <label htmlFor="roster-team">Rosa della squadra</label>
          <select
            id="roster-team"
            value={teamId}
            onChange={(event) => {
              onTeamChange(event.target.value)
              setPage(1)
            }}
          >
            {teams.map((team) => (
              <option key={team.id} value={team.id}>
                {team.name}
              </option>
            ))}
          </select>
        </div>
      )}
      {!history && team && rules && (
        <div className="roster-summary">
          <p>
            <strong>{team.budget}</strong> crediti disponibili{' '}
            <span>
              Offerta max {maxOffer(team.budget, emptySlots(team, rules))}
            </span>
          </p>
          <div className="roster-capacity">
            {roles.map((role) => (
              <p key={role.id}>
                <span className={`role-${role.id}`}>{role.label}</span>
                <strong>
                  {team[role.field]} / {rules[role.field]}
                </strong>
              </p>
            ))}
          </div>
        </div>
      )}
      {result.isPending ? (
        <LoadingState />
      ) : result.isError ? (
        <ErrorState error={result.error} retry={() => void result.refetch()} />
      ) : !result.data.items.length ? (
        <p className="auction-empty">
          {history
            ? 'Gli acquisti compariranno qui dopo le prime aggiudicazioni.'
            : 'La rosa è ancora tutta da costruire.'}
        </p>
      ) : (
        <div className="catalog-list">
          {result.data.items.map((entry) => (
            <div className="catalog-row" key={entry.playerId}>
              <PlayerPhoto url={entry.photoUrl} role={entry.role} />
              <div>
                <h3>{entry.name}</h3>
                <p className="player-club-line">
                  <span>{entry.role}</span>
                  <ClubLabel
                    name={entry.clubName}
                    logoUrl={entry.clubLogoUrl}
                  />
                  {history
                    ? ` · ${teams.find((team) => team.id === entry.teamId)?.name ?? 'Squadra'}`
                    : ''}
                </p>
              </div>
              <span className="roster-price">
                {entry.price}
                <small>crediti</small>
              </span>
            </div>
          ))}
        </div>
      )}
      {result.data && result.data.total > 100 && (
        <nav className="auction-pagination" aria-label="Pagine degli acquisti">
          <Button
            variant="ghost"
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
          >
            Precedente
          </Button>
          <span>
            {page} / {Math.ceil(result.data.total / 100)}
          </span>
          <Button
            variant="ghost"
            disabled={page * 100 >= result.data.total}
            onClick={() => setPage(page + 1)}
          >
            Successiva
          </Button>
        </nav>
      )}
    </div>
  )
}
