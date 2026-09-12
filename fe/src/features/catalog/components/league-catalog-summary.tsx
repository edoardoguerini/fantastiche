import { useQuery } from '@tanstack/react-query'
import { authQueryOptions } from '@/features/auth'
import { leagueCatalogOptions } from '../actions/catalog.queries'
import { versionDate } from './catalog-versions-list'

// Cella di riepilogo per la pagina lega: condivide la query del pannello
// listone, quindi non aggiunge richieste.
export function LeagueCatalogSummary({
  leagueId,
  seasonId,
}: {
  leagueId: string
  seasonId: string
}) {
  const auth = useQuery(authQueryOptions())
  const current = useQuery({
    ...leagueCatalogOptions(auth.data?.id ?? '', leagueId, seasonId),
    enabled: !!auth.data,
  })
  if (current.isError) return null
  return (
    <div className="league-summary-item" aria-busy={current.isPending}>
      <dt>Listone</dt>
      {current.isPending ? (
        <dd>…</dd>
      ) : current.data ? (
        <>
          <dd>
            {current.data.entryCount} <span>calciatori</span>
          </dd>
          <p>Versione del {versionDate(current.data)}</p>
        </>
      ) : (
        <>
          <dd>Da scegliere</dd>
          <p>Nessuna versione fissata</p>
        </>
      )}
    </div>
  )
}
