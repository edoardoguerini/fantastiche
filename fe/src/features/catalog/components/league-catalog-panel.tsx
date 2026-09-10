import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { Button } from '@/components/primitives/button'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { api } from '@/lib/api/client'
import {
  catalogKeys,
  leagueCatalogOptions,
  leagueCatalogPath,
  leagueCatalogPermissionOptions,
} from '../actions/catalog.queries'
import { leagueCatalogSchema, type ListVersion } from '../types/catalog.types'
import { useCatalogAction } from '../hooks/use-catalog-action'
import { CatalogVersionsList, versionDate } from './catalog-versions-list'
import { CatalogEntriesPreview } from './catalog-entries-preview'
import '../catalog.css'

export function LeagueCatalogPanel({
  leagueId,
  seasonId,
  seasonName,
}: {
  leagueId: string
  seasonId: string
  seasonName: string
}) {
  const auth = useQuery(authQueryOptions())
  if (!auth.data) return null
  return (
    <LeagueCatalogContent
      key={`${auth.data.id}:${leagueId}:${seasonId}`}
      userId={auth.data.id}
      isSuperAdmin={auth.data.isSuperAdmin}
      leagueId={leagueId}
      seasonId={seasonId}
      seasonName={seasonName}
    />
  )
}
function LeagueCatalogContent({
  userId,
  isSuperAdmin,
  leagueId,
  seasonId,
  seasonName,
}: {
  userId: string
  isSuperAdmin: boolean
  leagueId: string
  seasonId: string
  seasonName: string
}) {
  const client = useQueryClient()
  const current = useQuery(leagueCatalogOptions(userId, leagueId, seasonId))
  const permission = useQuery(
    leagueCatalogPermissionOptions(userId, leagueId, seasonId),
  )
  const action = useCatalogAction(userId)
  const [page, setPage] = useState(1)
  const [preview, setPreview] = useState<ListVersion | null>(null)
  const [choice, setChoice] = useState<ListVersion | null>(null)
  const refresh = async () => {
    await client.invalidateQueries({ queryKey: catalogKeys.all(userId) })
    if (action.isCurrent())
      await client.invalidateQueries({
        queryKey: ['auctions', userId, 'room', leagueId],
      })
  }
  const choose = async () => {
    if (
      !choice ||
      current.data ||
      !permission.data?.canManage ||
      current.isError ||
      permission.isError
    )
      return
    await action.run(
      async (signal) =>
        leagueCatalogSchema.parse(
          await api.put(
            leagueCatalogPath(leagueId, seasonId),
            { listVersionId: choice.id },
            signal,
          ),
        ),
      async () => {
        setChoice(null)
        setPreview(null)
        await refresh()
      },
    )
  }
  return (
    <section
      className="catalog-section league-catalog-panel"
      aria-labelledby="league-catalog-title"
    >
      <div className="catalog-section-heading">
        <div>
          <h2 id="league-catalog-title">Listone della lega</h2>
          <p>Stagione {seasonName}</p>
        </div>
        <div className="catalog-actions">
          <Button variant="ghost" onClick={() => void refresh()}>
            Aggiorna listoni
          </Button>
          {isSuperAdmin && (
            <Button asChild variant="outline">
              <Link to="/catalogo">Gestisci listoni</Link>
            </Button>
          )}
        </div>
      </div>
      {current.isPending ? (
        <LoadingState message="Caricamento listone della lega…" />
      ) : current.isError ? (
        <ErrorState
          error={current.error}
          retry={() => void current.refetch()}
        />
      ) : current.data ? (
        <>
          <p>
            <strong>{current.data.entryCount} calciatori</strong> · Versione del{' '}
            {versionDate(current.data)}
          </p>
          <p>
            Il listone scelto resta fissato per questa stagione e non può essere
            sostituito.
          </p>
          <Button
            variant="outline"
            onClick={() => setPreview(preview ? null : current.data)}
          >
            {' '}
            {preview ? 'Chiudi listone' : 'Consulta listone'}{' '}
          </Button>
        </>
      ) : permission.isPending ? (
        <LoadingState message="Verifica dei permessi…" />
      ) : permission.isError ? (
        <ErrorState
          error={permission.error}
          retry={() => void permission.refetch()}
        />
      ) : !permission.data.canManage ? (
        <p>
          L’organizzatore deve ancora scegliere un listone per questa stagione.
        </p>
      ) : (
        <>
          <p>
            Scegli una versione pubblicata per la stagione {seasonName}.
            Controlla i calciatori prima di confermare: la scelta non potrà
            essere sostituita.
          </p>
          <CatalogVersionsList
            userId={userId}
            seasonName={seasonName}
            page={page}
            onPage={setPage}
            onPreview={setPreview}
            onChoose={setChoice}
            publishedOnly
            disabled={action.busy}
          />
          {choice && (
            <div
              className="catalog-notice"
              role="group"
              aria-label="Conferma listone"
            >
              <p>
                Confermi il listone del {versionDate(choice)} con{' '}
                {choice.entryCount} calciatori? Questa sarà la versione fissata
                per la lega.
              </p>
              <div className="catalog-actions">
                <Button
                  variant="ghost"
                  disabled={action.busy}
                  onClick={() => setChoice(null)}
                >
                  Annulla
                </Button>
                <Button disabled={action.busy} onClick={() => void choose()}>
                  {action.busy
                    ? 'Scelta in corso…'
                    : 'Conferma scelta del listone'}
                </Button>
              </div>
            </div>
          )}
        </>
      )}
      {action.error && (
        <p className="form-error" role="alert">
          {action.error}
        </p>
      )}
      {!current.isError && !permission.isError && preview && (
        <CatalogEntriesPreview
          key={preview.id}
          userId={userId}
          version={preview}
        />
      )}
    </section>
  )
}
