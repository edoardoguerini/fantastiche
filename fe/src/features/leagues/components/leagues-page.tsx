import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { LeagueCard } from './league-card'
import { leaguesQueryOptions } from '../actions/leagues.queries'

export function LeaguesPage({ page }: { page: number }) {
  const { data: user } = useQuery(authQueryOptions())
  const leagues = useQuery({
    ...leaguesQueryOptions(user?.id ?? '', page),
    enabled: !!user,
  })
  return (
    <div className="content-container">
      <header className="page-heading page-heading-with-action">
        <div>
          <h1>{user?.isSuperAdmin ? 'Le leghe' : 'Le mie leghe'}</h1>
          <p>
            {user?.isSuperAdmin
              ? 'Le leghe e le stagioni di Fantastiche.'
              : 'Il prossimo capitolo della tua stagione parte da qui.'}
          </p>
        </div>
        {user?.isSuperAdmin && (
          <Button asChild>
            <Link to="/leghe/nuova">
              <Icon name="plus" />
              Crea lega
            </Link>
          </Button>
        )}
      </header>
      {leagues.isPending ? (
        <LoadingState message="Caricamento delle leghe…" />
      ) : leagues.isError ? (
        <ErrorState
          error={leagues.error}
          retry={() => void leagues.refetch()}
        />
      ) : leagues.data.items.length === 0 ? (
        <section className="empty-leagues">
          <div className="empty-emblem" aria-hidden="true">
            <Icon name="trophy" />
          </div>
          <h2>
            {page > 1
              ? 'Nessuna lega in questa pagina'
              : 'La tua stagione deve ancora cominciare'}
          </h2>
          <p>
            {page > 1
              ? 'Torna alla prima pagina per vedere le leghe disponibili.'
              : user?.isSuperAdmin
                ? 'Non ci sono ancora leghe disponibili.'
                : 'Non fai ancora parte di una lega attiva. Quando ricevi un invito, accettalo per ritrovarla qui.'}
          </p>
          {page > 1 && (
            <Button asChild variant="outline">
              <Link to="/leghe" search={{ page: 1 }}>
                Torna alle leghe
              </Link>
            </Button>
          )}
        </section>
      ) : (
        <>
          <div className="league-list">
            {leagues.data.items.map((league) => (
              <LeagueCard key={league.leagueSeasonId} league={league} />
            ))}
          </div>
          {leagues.data.totalCount > leagues.data.pageSize && (
            <nav className="pagination" aria-label="Pagine delle leghe">
              {page > 1 ? (
                <Button asChild variant="outline">
                  <Link to="/leghe" search={{ page: page - 1 }}>
                    Precedente
                  </Link>
                </Button>
              ) : (
                <Button variant="outline" disabled>
                  Precedente
                </Button>
              )}
              <span>
                Pagina {page} di{' '}
                {Math.ceil(leagues.data.totalCount / leagues.data.pageSize)}
              </span>
              {page * leagues.data.pageSize < leagues.data.totalCount ? (
                <Button asChild variant="outline">
                  <Link to="/leghe" search={{ page: page + 1 }}>
                    Successiva
                  </Link>
                </Button>
              ) : (
                <Button variant="outline" disabled>
                  Successiva
                </Button>
              )}
            </nav>
          )}
        </>
      )}
    </div>
  )
}
