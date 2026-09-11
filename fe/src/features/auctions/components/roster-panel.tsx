import { useInfiniteQuery } from '@tanstack/react-query'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { rosterQueryOptions, loadedPlayers } from '../actions/auction.queries'
import type { AuctionTeam } from '../types/auction.types'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'
import { InfiniteScrollMore } from './infinite-scroll-more'

export function RosterPanel({
  userId,
  sessionId,
  teams,
}: {
  userId: string
  sessionId: string
  teams: AuctionTeam[]
}) {
  const result = useInfiniteQuery(rosterQueryOptions(userId, sessionId))
  const entries = loadedPlayers(result.data?.pages)
  return (
    <div className="auction-roster-panel">
      {result.isPending ? (
        <LoadingState />
      ) : result.isError && !result.data ? (
        <ErrorState error={result.error} retry={() => void result.refetch()} />
      ) : !entries.length ? (
        <p className="auction-empty">
          Gli acquisti compariranno qui dopo le prime aggiudicazioni.
        </p>
      ) : (
        <div className="catalog-list">
          {entries.map((entry) => (
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
                  {` · ${teams.find((team) => team.id === entry.teamId)?.name ?? 'Squadra'}`}
                </p>
              </div>
              <span className="roster-price">
                {entry.price}
                <small>crediti</small>
              </span>
            </div>
          ))}
          <InfiniteScrollMore
            hasMore={result.hasNextPage}
            fetching={result.isFetching}
            error={result.isError}
            label="Carica altri acquisti"
            onLoad={() => {
              if (result.isFetching) return
              if (result.isRefetchError) void result.refetch()
              else void result.fetchNextPage({ cancelRefetch: false })
            }}
          />
        </div>
      )}
    </div>
  )
}
