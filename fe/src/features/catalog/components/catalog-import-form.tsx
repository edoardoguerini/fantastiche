import { useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { useHydrated } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import { api } from '@/lib/api/client'
import { ApiError } from '@/lib/api/error'
import { importCatalogSchema } from '../validations/catalog.validations'
import { useCatalogAction } from '../hooks/use-catalog-action'
import { listVersionSchema, type ListVersion } from '../types/catalog.types'

export function CatalogImportForm({
  userId,
  onImported,
}: {
  userId: string
  onImported: (version: ListVersion) => void | Promise<void>
}) {
  const hydrated = useHydrated()
  const action = useCatalogAction(userId)
  const [imported, setImported] = useState(false)
  const [fileKey, setFileKey] = useState(0)
  const form = useForm({
    defaultValues: { seasonName: '', file: null as File | null },
    validators: { onSubmit: importCatalogSchema },
    onSubmit: async ({ value }) => {
      if (imported || !value.file) return
      await action.run(
        async (signal) => {
          // Leggiamo il contenuto originale: il parser autorevole delle 19 colonne è sul server.
          if (value.file!.size > 4_194_304)
            throw new ApiError(
              400,
              'catalog.file_size',
              'Il file è troppo grande: il limite è 1.048.576 caratteri.',
            )
          const csv = await value.file!.text()
          if (!csv.trim() || csv.length > 1_048_576)
            throw new ApiError(
              400,
              'catalog.file_size',
              'Scegli un CSV non vuoto con al massimo 1.048.576 caratteri.',
            )
          return listVersionSchema.parse(
            await api.post(
              '/Catalog/Imports',
              { seasonName: value.seasonName.trim(), csv },
              signal,
            ),
          )
        },
        async (version) => {
          setImported(true)
          await onImported(version)
        },
      )
    },
  })
  return (
    <section className="catalog-section" aria-labelledby="catalog-import-title">
      <h2 id="catalog-import-title">Importa un listone</h2>
      <p>
        Seleziona il CSV originale Fantacalcio a 19 colonne, senza intestazione.
        Controlla la bozza prima di pubblicarla.
      </p>
      <form
        className="catalog-form"
        method="post"
        noValidate
        onSubmit={(event) => {
          event.preventDefault()
          void form.handleSubmit()
        }}
      >
        <form.Field name="seasonName">
          {(field) => (
            <div className="form-field">
              <label htmlFor="catalog-import-season">
                Stagione del listone
              </label>
              <Input
                id="catalog-import-season"
                placeholder="Es. 2026/27"
                value={field.state.value}
                maxLength={50}
                disabled={!hydrated || action.busy || imported}
                onChange={(event) => field.handleChange(event.target.value)}
                onBlur={field.handleBlur}
                aria-invalid={field.state.meta.errors.length > 0}
                aria-describedby={
                  field.state.meta.errors.length
                    ? 'catalog-season-error'
                    : undefined
                }
              />
              {field.state.meta.errors[0] && (
                <p
                  id="catalog-season-error"
                  className="field-error"
                  role="alert"
                >
                  {field.state.meta.errors[0].message}
                </p>
              )}
            </div>
          )}
        </form.Field>
        <form.Field name="file">
          {(field) => (
            <div className="form-field">
              <label htmlFor="catalog-import-file">File CSV Fantacalcio</label>
              <Input
                key={fileKey}
                id="catalog-import-file"
                type="file"
                accept=".csv,text/csv"
                disabled={!hydrated || action.busy || imported}
                onChange={(event) =>
                  field.handleChange(event.target.files?.[0] ?? null)
                }
                aria-invalid={field.state.meta.errors.length > 0}
                aria-describedby="catalog-file-help"
              />
              <p id="catalog-file-help">
                Massimo 1.048.576 caratteri e 5.000 calciatori. Il file viene
                inviato solo quando confermi l’importazione.
              </p>
              {field.state.meta.errors[0] && (
                <p className="field-error" role="alert">
                  {field.state.meta.errors[0].message}
                </p>
              )}
            </div>
          )}
        </form.Field>
        {action.error && (
          <p className="form-error" role="alert">
            {action.error}
          </p>
        )}
        {imported ? (
          <p role="status">
            Listone importato. Puoi consultarlo nell’anteprima.
          </p>
        ) : (
          <Button type="submit" disabled={!hydrated || action.busy}>
            {action.busy ? 'Importazione in corso…' : 'Importa in bozza'}
          </Button>
        )}
        {imported && (
          <Button
            variant="outline"
            onClick={() => {
              setImported(false)
              form.reset()
              setFileKey((value) => value + 1)
            }}
          >
            Importa un altro file
          </Button>
        )}
      </form>
    </section>
  )
}
