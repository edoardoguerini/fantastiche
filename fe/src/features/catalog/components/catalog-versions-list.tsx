import { useQuery } from '@tanstack/react-query'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { Button } from '@/components/primitives/button'
import { catalogVersionsOptions } from '../actions/catalog.queries'
import type { ListVersion } from '../types/catalog.types'
import { CatalogPagination } from './catalog-pagination'

export function versionDate(version: ListVersion) {
  return new Intl.DateTimeFormat('it-IT', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(version.createdAt))
}
export function CatalogVersionsList({
  userId,
  seasonName,
  page,
  onPage,
  onPreview,
  onChoose,
  publishedOnly = false,
  disabled = false,
}: {
  userId: string
  seasonName: string
  page: number
  onPage: (page: number) => void
  onPreview: (version: ListVersion) => void
  onChoose?: (version: ListVersion) => void
  publishedOnly?: boolean
  disabled?: boolean
}) {
  const result = useQuery(catalogVersionsOptions(userId, seasonName, page))
  if (result.isPending) return <LoadingState message="Caricamento listoni…" />
  if (result.isError)
    return (
      <ErrorState error={result.error} retry={() => void result.refetch()} />
    )
  const versions = result.data.items.filter(
    (item) =>
      !publishedOnly ||
      (item.status === 'Published' && item.seasonName === seasonName),
  )
  return (
    <>
      {versions.length ? (
        <ul className="catalog-version-list">
          {versions.map((version) => (
            <li key={version.id}>
              <div>
                <strong>Stagione {version.seasonName}</strong>
                <p>
                  {version.entryCount} calciatori · {versionDate(version)}
                </p>
                <small>
                  {version.status === 'Published' ? 'Pubblicato' : 'Bozza'} ·
                  Versione {version.id.slice(0, 8)}
                </small>
              </div>
              <div className="catalog-actions">
                <Button variant="outline" onClick={() => onPreview(version)}>
                  Consulta
                </Button>
                {onChoose && (
                  <Button disabled={disabled} onClick={() => onChoose(version)}>
                    Scegli questo listone
                  </Button>
                )}
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <p>
          {publishedOnly
            ? 'Nessun listone pubblicato per questa stagione in questa pagina.'
            : 'Non ci sono listoni con questi filtri.'}
        </p>
      )}
      <CatalogPagination
        label="Pagine delle versioni"
        page={page}
        pageSize={10}
        total={result.data.total}
        onPage={onPage}
      />
    </>
  )
}
