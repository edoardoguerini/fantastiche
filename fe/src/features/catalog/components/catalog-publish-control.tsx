import { Button } from '@/components/primitives/button'
import { api } from '@/lib/api/client'
import { useCatalogAction } from '../hooks/use-catalog-action'
import { listVersionSchema, type ListVersion } from '../types/catalog.types'

export function CatalogPublishControl({
  userId,
  version,
  onPublished,
}: {
  userId: string
  version: ListVersion
  onPublished: (version: ListVersion) => void | Promise<void>
}) {
  const action = useCatalogAction(userId)
  if (version.status === 'Published')
    return (
      <p className="catalog-notice">
        Questo listone è pubblicato e può essere scelto dalle leghe della
        stagione {version.seasonName}.
      </p>
    )
  return (
    <div className="catalog-notice">
      <p>
        La bozza è visibile solo agli amministratori. Pubblicandola, la rendi
        disponibile alle leghe della stagione {version.seasonName}. Le leghe che
        hanno già scelto un listone conservano la propria versione.
      </p>
      {action.error && (
        <p role="alert" className="form-error">
          {action.error}
        </p>
      )}
      <Button
        disabled={action.busy}
        onClick={() =>
          void action.run(
            async (signal) =>
              listVersionSchema.parse(
                await api.post(
                  `/Catalog/Versions/${version.id}/Publish`,
                  {},
                  signal,
                ),
              ),
            onPublished,
          )
        }
      >
        {action.busy ? 'Pubblicazione in corso…' : 'Pubblica listone'}
      </Button>
    </div>
  )
}
