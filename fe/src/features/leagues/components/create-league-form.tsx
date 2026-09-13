import { useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useHydrated } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import { ApiError, errorMessage } from '@/lib/api/error'
import { createLeagueMutationOptions } from '../actions/league.mutations'
import { leagueQueryOptions } from '../actions/leagues.queries'
import { createLeagueSchema } from '../validations/league.validations'
import { LeagueLogoDropzone } from './league-logo-dropzone'
import { leagueLogoError } from '../utils/league-logo-file'
import type { League } from '../types/leagues.types'

const textSections = [
  {
    title: 'La lega',
    fields: [
      {
        name: 'name',
        label: 'Nome della lega',
        placeholder: 'Es. Lega del mercoledì',
        max: 100,
        type: 'text',
      },
      {
        name: 'seasonName',
        label: 'Stagione',
        placeholder: 'Es. 2026/27',
        max: 50,
        type: 'text',
      },
    ],
  },
  {
    title: 'L’organizzatore',
    description:
      'La persona che gestirà la lega riceverà un invito a questo indirizzo.',
    fields: [
      {
        name: 'organizerEmail',
        label: 'Email dell’organizzatore',
        placeholder: 'nome@esempio.it',
        max: 256,
        type: 'email',
      },
    ],
  },
] as const
const numberFields = [
  { name: 'budget', label: 'Budget per squadra', min: 1, max: 1_000_000 },
  { name: 'goalkeepers', label: 'Portieri', min: 0, max: 100 },
  { name: 'defenders', label: 'Difensori', min: 0, max: 100 },
  { name: 'midfielders', label: 'Centrocampisti', min: 0, max: 100 },
  { name: 'forwards', label: 'Attaccanti', min: 0, max: 100 },
] as const

export function CreateLeagueForm({
  userId,
  onSuccess,
}: {
  userId: string
  onSuccess: (league: League) => void | Promise<void>
}) {
  const hydrated = useHydrated()
  const [logoFile, setLogoFile] = useState<File | null>(null)
  const logoError = leagueLogoError(logoFile)
  const client = useQueryClient()
  const create = useMutation(createLeagueMutationOptions())
  const submitting = useRef(false)
  const [submitError, setSubmitError] = useState<unknown>(null)
  const [created, setCreated] = useState(false)
  const form = useForm({
    defaultValues: {
      name: '',
      seasonName: '',
      organizerEmail: '',
      budget: 500,
      goalkeepers: 3,
      defenders: 8,
      midfielders: 8,
      forwards: 6,
    },
    validators: { onSubmit: createLeagueSchema },
    onSubmit: async ({ value }) => {
      if (submitting.current || created || logoError) return
      submitting.current = true
      setSubmitError(null)
      try {
        const league = await create.mutateAsync({ ...value, logoFile })
        setCreated(true)
        // Una risposta tardiva non deve ripopolare la cache di un'altra sessione.
        const currentUser = client.getQueryData(authQueryOptions().queryKey)
        if (currentUser?.id !== userId || !currentUser.isSuperAdmin) return
        await client.cancelQueries({ queryKey: ['leagues', userId] })
        if (client.getQueryData(authQueryOptions().queryKey)?.id !== userId)
          return
        client.setQueryData(
          leagueQueryOptions(userId, league.id).queryKey,
          league,
        )
        await client.invalidateQueries({
          queryKey: ['leagues', userId, 'list'],
        })
        if (client.getQueryData(authQueryOptions().queryKey)?.id !== userId)
          return
        await onSuccess(league)
      } catch (error) {
        setSubmitError(error)
        if (
          error instanceof ApiError &&
          (error.status === 401 || error.status === 403)
        )
          await client.invalidateQueries({
            queryKey: authQueryOptions().queryKey,
          })
      } finally {
        submitting.current = false
      }
    },
  })

  return (
    <form
      className="create-league-form"
      method="post"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        const element = event.currentTarget
        void form.handleSubmit().then(() => {
          element
            .querySelector<HTMLInputElement>('[aria-invalid="true"]')
            ?.focus()
        })
      }}
    >
      <form.Subscribe selector={(state) => state.isSubmitting}>
        {(isSubmitting) => (
          <fieldset
            className="league-form-fields"
            disabled={!hydrated || isSubmitting || created}
          >
            {textSections.map((section) => (
              <section
                className="league-form-section"
                key={section.title}
                aria-label={section.title}
              >
                <h2>{section.title}</h2>
                {'description' in section && (
                  <p className="form-description">{section.description}</p>
                )}
                <div className="league-form-grid">
                  {section.fields.map((config) => (
                    <form.Field key={config.name} name={config.name}>
                      {(field) => {
                        const error = field.state.meta.errors[0]?.message
                        return (
                          <div className="form-field">
                            <label htmlFor={config.name}>{config.label}</label>
                            <Input
                              id={config.name}
                              name={config.name}
                              type={config.type}
                              maxLength={config.max}
                              placeholder={config.placeholder}
                              autoComplete="off"
                              value={field.state.value}
                              onChange={(event) =>
                                field.handleChange(event.target.value)
                              }
                              onBlur={() => {
                                field.handleChange(field.state.value.trim())
                                field.handleBlur()
                              }}
                              aria-invalid={!!error}
                              aria-describedby={
                                error ? `${config.name}-error` : undefined
                              }
                            />
                            {error && (
                              <p
                                className="field-error"
                                role="alert"
                                id={`${config.name}-error`}
                              >
                                {error}
                              </p>
                            )}
                          </div>
                        )
                      }}
                    </form.Field>
                  ))}
                </div>
                {section.title === 'La lega' && (
                  <LeagueLogoDropzone
                    file={logoFile}
                    onChange={setLogoFile}
                    disabled={!hydrated || isSubmitting || created}
                  />
                )}
              </section>
            ))}
            <section
              className="league-form-section"
              aria-labelledby="league-rules-title"
            >
              <h2 id="league-rules-title">Le regole</h2>
              <p className="form-description">
                Crediti iniziali e composizione della rosa per ogni squadra.
              </p>
              <div className="league-rules-grid">
                {numberFields.map((config) => (
                  <form.Field key={config.name} name={config.name}>
                    {(field) => {
                      const error = field.state.meta.errors[0]?.message
                      return (
                        <div
                          className={`form-field ${config.name === 'budget' ? 'league-budget' : ''}`}
                        >
                          <label htmlFor={config.name}>{config.label}</label>
                          <Input
                            id={config.name}
                            name={config.name}
                            type="number"
                            inputMode="numeric"
                            min={config.min}
                            max={config.max}
                            step={1}
                            value={
                              Number.isNaN(field.state.value)
                                ? ''
                                : field.state.value
                            }
                            onChange={(event) =>
                              field.handleChange(event.target.valueAsNumber)
                            }
                            onBlur={field.handleBlur}
                            aria-invalid={!!error}
                            aria-describedby={
                              error ? `${config.name}-error` : undefined
                            }
                          />
                          {error && (
                            <p
                              className="field-error"
                              role="alert"
                              id={`${config.name}-error`}
                            >
                              {error}
                            </p>
                          )}
                        </div>
                      )
                    }}
                  </form.Field>
                ))}
              </div>
              <form.Subscribe
                selector={(state) => [
                  state.values.goalkeepers,
                  state.values.defenders,
                  state.values.midfielders,
                  state.values.forwards,
                ]}
              >
                {(values) => (
                  <p className="roster-total" aria-live="polite">
                    Totale rosa{' '}
                    <strong>
                      {values.every(Number.isFinite)
                        ? values.reduce((sum, value) => sum + value, 0)
                        : '—'}
                    </strong>{' '}
                    giocatori
                  </p>
                )}
              </form.Subscribe>
            </section>
          </fieldset>
        )}
      </form.Subscribe>
      {submitError !== null && (
        <p className="form-error" role="alert">
          {errorMessage(submitError)}
        </p>
      )}
      <div className="league-form-actions">
        <p>Creando la lega, verrà preparato l’invito per l’organizzatore.</p>
        <form.Subscribe selector={(state) => state.isSubmitting}>
          {(isSubmitting) => (
            <Button
              type="submit"
              disabled={!hydrated || isSubmitting || created || !!logoError}
              aria-busy={isSubmitting}
            >
              {created
                ? 'Lega creata'
                : isSubmitting
                  ? 'Creazione in corso…'
                  : 'Crea lega'}
            </Button>
          )}
        </form.Subscribe>
      </div>
    </form>
  )
}
