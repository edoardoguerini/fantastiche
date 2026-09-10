import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { ParticipantsPanel } from '@/features/invitations'
import { LeagueCatalogPanel } from '@/features/catalog'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { leagueQueryOptions } from '../actions/leagues.queries'

export function LeaguePage({ leagueId }: { leagueId: string }) {
  const { data: user } = useQuery(authQueryOptions())
  const result = useQuery({
    ...leagueQueryOptions(user?.id ?? '', leagueId),
    enabled: !!user,
  })
  const league = result.data
  return (
    <div className="content-container">
      <Link className="back-link" to="/leghe">
        <Icon name="arrow-left" />
        Torna alle leghe
      </Link>
      {result.isPending ? (
        <LoadingState />
      ) : result.isError ? (
        <ErrorState error={result.error} retry={() => void result.refetch()} />
      ) : (
        league && (
          <>
            <header className="page-heading">
              <p>Stagione {league.seasonName}</p>
              <h1>{league.name}</h1>
              <p>Le regole della tua lega, prima di scendere in campo.</p>
            </header>
            <section className="league-details" aria-labelledby="rules-title">
              <h2 id="rules-title">Configurazione della rosa</h2>
              <dl>
                <div>
                  <dt>Budget iniziale</dt>
                  <dd>
                    {league.budget} <span>crediti</span>
                  </dd>
                </div>
                <div>
                  <dt>Portieri</dt>
                  <dd>{league.goalkeepers}</dd>
                </div>
                <div>
                  <dt>Difensori</dt>
                  <dd>{league.defenders}</dd>
                </div>
                <div>
                  <dt>Centrocampisti</dt>
                  <dd>{league.midfielders}</dd>
                </div>
                <div>
                  <dt>Attaccanti</dt>
                  <dd>{league.forwards}</dd>
                </div>
              </dl>
            </section>
            <div className="section-note">
              <Button asChild>
                <Link to="/leghe/$leagueId/asta" params={{ leagueId }}>
                  <Icon name="play" />
                  Entra nella sala d’asta
                </Link>
              </Button>
            </div>
            <LeagueCatalogPanel
              leagueId={league.id}
              seasonId={league.leagueSeasonId}
              seasonName={league.seasonName}
            />
            {user && (
              <ParticipantsPanel
                key={`${user.id}:${league.leagueSeasonId}`}
                userId={user.id}
                leagueId={league.id}
                seasonId={league.leagueSeasonId}
              />
            )}
          </>
        )
      )}
    </div>
  )
}
