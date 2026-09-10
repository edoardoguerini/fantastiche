import { useId, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { useQuery } from '@tanstack/react-query'
import { z } from 'zod'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { catalogEntriesOptions } from '../actions/catalog.queries'
import type { CatalogFilters, ListVersion } from '../types/catalog.types'
import { CatalogPagination } from './catalog-pagination'
import { CatalogPlayerImage } from './catalog-player-image'

const filtersSchema = z.object({
  search: z.string().max(100),
  club: z.string().max(100),
  role: z.enum(['', 'P', 'D', 'C', 'A']),
})
export function CatalogEntriesPreview({
  userId,
  version,
}: {
  userId: string
  version: ListVersion
}) {
  const id = useId()
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<CatalogFilters>({
    search: '',
    role: '',
    club: '',
  })
  const form = useForm({
    defaultValues: {
      search: '',
      role: '' as '' | 'P' | 'D' | 'C' | 'A',
      club: '',
    },
    validators: { onSubmit: filtersSchema },
    onSubmit: ({ value }) => {
      setFilters({
        ...value,
        search: value.search.trim(),
        club: value.club.trim(),
      })
      setPage(1)
    },
  })
  const result = useQuery(
    catalogEntriesOptions(userId, version.id, filters, page),
  )
  return (
    <section
      className="catalog-section catalog-preview"
      aria-label="Anteprima del listone"
    >
      <div className="catalog-section-heading">
        <div>
          <h3>Calciatori · {version.seasonName}</h3>
          <p>
            {version.entryCount} calciatori ·{' '}
            {version.status === 'Published' ? 'Pubblicato' : 'Bozza'}
          </p>
        </div>
      </div>
      <form
        className="catalog-filters"
        onSubmit={(event) => {
          event.preventDefault()
          void form.handleSubmit()
        }}
      >
        <form.Field name="search">
          {(field) => (
            <div className="form-field">
              <label htmlFor={`${id}-search`}>Cerca calciatore</label>
              <Input
                id={`${id}-search`}
                maxLength={100}
                value={field.state.value}
                onChange={(event) => field.handleChange(event.target.value)}
                placeholder="Nome o cognome"
              />
            </div>
          )}
        </form.Field>
        <form.Field name="role">
          {(field) => (
            <div className="form-field">
              <label htmlFor={`${id}-role`}>Ruolo</label>
              <select
                id={`${id}-role`}
                value={field.state.value}
                onChange={(event) =>
                  field.handleChange(
                    event.target.value as typeof field.state.value,
                  )
                }
              >
                <option value="">Tutti i ruoli</option>
                <option value="P">Portieri</option>
                <option value="D">Difensori</option>
                <option value="C">Centrocampisti</option>
                <option value="A">Attaccanti</option>
              </select>
            </div>
          )}
        </form.Field>
        <form.Field name="club">
          {(field) => (
            <div className="form-field">
              <label htmlFor={`${id}-club`}>Club</label>
              <Input
                id={`${id}-club`}
                maxLength={100}
                value={field.state.value}
                onChange={(event) => field.handleChange(event.target.value)}
                placeholder="Nome del club"
              />
            </div>
          )}
        </form.Field>
        <Button variant="outline" type="submit">
          Filtra
        </Button>
        <Button
          variant="ghost"
          onClick={() => {
            form.reset()
            setFilters({ search: '', role: '', club: '' })
            setPage(1)
          }}
        >
          Azzera filtri
        </Button>
      </form>
      {result.isPending ? (
        <LoadingState message="Caricamento calciatori…" />
      ) : result.isError ? (
        <ErrorState error={result.error} retry={() => void result.refetch()} />
      ) : (
        <>
          <p className="catalog-result-count" role="status">
            {result.data.total} risultati
          </p>
          {!result.data.items.length ? (
            <p>Nessun calciatore corrisponde ai filtri.</p>
          ) : (
            <ul className="catalog-player-list">
              {result.data.items.map((entry) => (
                <li key={entry.playerId}>
                  <CatalogPlayerImage
                    key={entry.photoUrl ?? entry.playerId}
                    src={entry.photoUrl}
                    kind="player"
                    fallback={entry.role}
                  />
                  <div>
                    <strong>{entry.name}</strong>
                    {entry.isTransferred && (
                      <span className="catalog-transferred">Ceduto</span>
                    )}
                    <p className="catalog-player-club">
                      <CatalogPlayerImage
                        key={entry.clubLogoUrl ?? entry.clubName}
                        src={entry.clubLogoUrl}
                        kind="club"
                      />
                      {entry.clubName}
                    </p>
                    <p className="catalog-market-data">
                      {entry.currentQuotation != null && (
                        <span>Qt. {entry.currentQuotation}</span>
                      )}
                      {entry.fvm != null && <span>FVM {entry.fvm}</span>}
                    </p>
                  </div>
                  <span
                    className="catalog-role"
                    aria-label={`Ruolo ${entry.role}`}
                  >
                    {entry.role}
                  </span>
                </li>
              ))}
            </ul>
          )}
          <CatalogPagination
            label="Pagine dei calciatori"
            page={page}
            pageSize={30}
            total={result.data.total}
            onPage={setPage}
          />
        </>
      )}
    </section>
  )
}
