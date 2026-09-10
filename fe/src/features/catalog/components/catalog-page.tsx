import { useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { LoadingState } from '@/components/common/page-state'
import { Input } from '@/components/primitives/input'
import { Button } from '@/components/primitives/button'
import { catalogKeys } from '../actions/catalog.queries'
import type { ListVersion } from '../types/catalog.types'
import { CatalogImportForm } from './catalog-import-form'
import { CatalogVersionsList } from './catalog-versions-list'
import { CatalogEntriesPreview } from './catalog-entries-preview'
import { CatalogPublishControl } from './catalog-publish-control'
import '../catalog.css'

export function CatalogPage() {
  const auth = useQuery(authQueryOptions())
  if (auth.isPending) return <LoadingState />
  if (!auth.data?.isSuperAdmin)
    return (
      <div className="content-container">
        <h1>Accesso riservato</h1>
        <p>La gestione del catalogo è riservata al SuperAdmin.</p>
        <Link to="/leghe">Torna alle leghe</Link>
      </div>
    )
  return <CatalogWorkspace key={auth.data.id} userId={auth.data.id} />
}
function CatalogWorkspace({ userId }: { userId: string }) {
  const client = useQueryClient()
  const [seasonFilter, setSeasonFilter] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<ListVersion | null>(null)
  const filterForm = useForm({
    defaultValues: { seasonName: '' },
    validators: { onSubmit: z.object({ seasonName: z.string().max(50) }) },
    onSubmit: ({ value }) => {
      setSeasonFilter(value.seasonName.trim())
      setPage(1)
    },
  })
  const refresh = async () => {
    await client.invalidateQueries({ queryKey: catalogKeys.all(userId) })
  }
  const updated = async (version: ListVersion) => {
    if (client.getQueryData(authQueryOptions().queryKey)?.id !== userId) return
    setSelected(version)
    await refresh()
  }
  return (
    <div className="content-container catalog-admin">
      <Link className="back-link" to="/leghe">
        Torna alle leghe
      </Link>
      <header className="page-heading">
        <p>AMMINISTRAZIONE</p>
        <h1>Listoni</h1>
        <p>
          Importa, controlla e pubblica i calciatori disponibili per le leghe.
        </p>
      </header>
      <CatalogImportForm userId={userId} onImported={updated} />
      <section
        className="catalog-section"
        aria-labelledby="catalog-versions-title"
      >
        <div className="catalog-section-heading">
          <h2 id="catalog-versions-title">Versioni del listone</h2>
          <Button variant="ghost" onClick={() => void refresh()}>
            Aggiorna listoni
          </Button>
        </div>
        <form
          className="catalog-season-filter"
          onSubmit={(event) => {
            event.preventDefault()
            void filterForm.handleSubmit()
          }}
        >
          <filterForm.Field name="seasonName">
            {(field) => (
              <div className="form-field">
                <label htmlFor="catalog-version-season">
                  Filtra per stagione
                </label>
                <Input
                  id="catalog-version-season"
                  placeholder="Tutte le stagioni"
                  value={field.state.value}
                  maxLength={50}
                  onChange={(event) => field.handleChange(event.target.value)}
                />
              </div>
            )}
          </filterForm.Field>
          <Button variant="outline" type="submit">
            Filtra versioni
          </Button>
        </form>
        <CatalogVersionsList
          userId={userId}
          seasonName={seasonFilter}
          page={page}
          onPage={setPage}
          onPreview={setSelected}
        />
      </section>
      {selected && (
        <div className="catalog-selected" key={selected.id}>
          <div className="catalog-section-heading">
            <h2>Versione {selected.id.slice(0, 8)}</h2>
            <Button variant="ghost" onClick={() => setSelected(null)}>
              Chiudi anteprima
            </Button>
          </div>
          <CatalogEntriesPreview userId={userId} version={selected} />
          <CatalogPublishControl
            userId={userId}
            version={selected}
            onPublished={updated}
          />
        </div>
      )}
    </div>
  )
}
