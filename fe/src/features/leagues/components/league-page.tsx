import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { ParticipantsPanel, ParticipantsSummary } from '@/features/invitations'
import { LeagueCatalogPanel, LeagueCatalogSummary } from '@/features/catalog'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { leagueQueryOptions } from '../actions/leagues.queries'
import { AuctionStatus } from './auction-status'
import { LeagueLogo } from './league-logo'

export function LeaguePage({ leagueId }: { leagueId: string }) {
  const { data: user } = useQuery(authQueryOptions())
  const result = useQuery({
    ...leagueQueryOptions(user?.id ?? '', leagueId),
    enabled: !!user,
  })
  const league = result.data
  const rosterSize = league
    ? league.goalkeepers +
      league.defenders +
      league.midfielders +
      league.forwards
    : 0
  return (
    <div className="content-container league-page">
      <Button
        asChild
        variant="outline"
        className="league-back-link size-11 rounded-full p-0"
      >
        <Link
          to="/leghe"
          aria-label="Torna alle leghe"
          title="Torna alle leghe"
        >
          <Icon name="chevron-left" />
        </Link>
      </Button>
      {result.isPending ? (
        <LoadingState />
      ) : result.isError ? (
        <ErrorState error={result.error} retry={() => void result.refetch()} />
      ) : (
        league &&
        user && (
          <>
            <header className="league-header">
              <div className="league-identity">
                <LeagueLogo name={league.name} url={league.logoUrl} />
                <div className="league-identity-text">
                  <p className="league-eyebrow">Stagione {league.seasonName}</p>
                  <h1>{league.name}</h1>
                  <p className="league-meta">
                    {league.auctionStatus && (
                      <AuctionStatus status={league.auctionStatus} />
                    )}
                    {league.myTeamName && (
                      <span>
                        La tua squadra: <strong>{league.myTeamName}</strong>
                      </span>
                    )}
                  </p>
                </div>
              </div>
              <Button asChild className="league-header-action">
                <Link to="/leghe/$leagueId/asta" params={{ leagueId }}>
                  {league.auctionStatus === 'Completed'
                    ? 'Rivedi l’asta'
                    : 'Entra nella sala d’asta'}
                </Link>
              </Button>
            </header>
            <dl className="league-summary" aria-label="Regole della lega">
              <div className="league-summary-item">
                <dt>Budget iniziale</dt>
                <dd>
                  {league.budget} <span>crediti</span>
                </dd>
              </div>
              <div className="league-summary-item">
                <dt>Rosa</dt>
                <dd>
                  {rosterSize} <span>giocatori</span>
                </dd>
                <p className="league-summary-roles">
                  <span>
                    {league.goalkeepers}{' '}
                    <abbr title="Portieri" aria-label="Portieri">
                      P
                    </abbr>
                  </span>
                  <span>
                    {league.defenders}{' '}
                    <abbr title="Difensori" aria-label="Difensori">
                      D
                    </abbr>
                  </span>
                  <span>
                    {league.midfielders}{' '}
                    <abbr title="Centrocampisti" aria-label="Centrocampisti">
                      C
                    </abbr>
                  </span>
                  <span>
                    {league.forwards}{' '}
                    <abbr title="Attaccanti" aria-label="Attaccanti">
                      A
                    </abbr>
                  </span>
                </p>
              </div>
              <LeagueCatalogSummary
                leagueId={league.id}
                seasonId={league.leagueSeasonId}
              />
              <ParticipantsSummary
                userId={user.id}
                leagueId={league.id}
                seasonId={league.leagueSeasonId}
              />
            </dl>
            <div className="league-layout">
              <ParticipantsPanel
                key={`${user.id}:${league.leagueSeasonId}`}
                userId={user.id}
                leagueId={league.id}
                seasonId={league.leagueSeasonId}
              />
              <LeagueCatalogPanel
                leagueId={league.id}
                seasonId={league.leagueSeasonId}
                seasonName={league.seasonName}
              />
            </div>
          </>
        )
      )}
    </div>
  )
}
