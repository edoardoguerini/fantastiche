import { PlayerValuation } from './player-valuation'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
import { Button } from '@/components/primitives/button'
import type { CatalogEntry } from '../types/auction.types'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'

const durations = [5, 10, 15, 20, 25, 30]
const incrementOptions = [1, 2, 5, 10, 20, 50]
const schema = z.object({
  duration: z
    .number()
    .refine((value) => durations.includes(value), 'Scegli una durata valida.'),
  increments: z
    .array(z.number().refine((value) => incrementOptions.includes(value)))
    .min(1, 'Seleziona almeno un incremento.'),
})
export function PlayerCallForm({
  player,
  disabled,
  onCancel,
  onStart,
}: {
  player: CatalogEntry
  disabled: boolean
  onCancel: () => void
  onStart: (duration: number, increments: number[]) => Promise<void>
}) {
  const form = useForm({
    defaultValues: { duration: 15, increments: [1, 5, 10] },
    validators: { onSubmit: schema },
    onSubmit: async ({ value }) => {
      if (!disabled) await onStart(value.duration, value.increments)
    },
  })
  return (
    <form
      className="player-call-form auction-stage auction-stage--preview"
      data-role={player.role}
      aria-label="Anteprima chiamata"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        void form.handleSubmit()
      }}
    >
      <div className="auction-stage-top">
        <span className="auction-eyebrow">La tua selezione</span>
      </div>
      <div className="player-call-heading">
        <PlayerPhoto url={player.photoUrl} role={player.role} large />
        <div>
          <span className={`catalog-role role-${player.role}`}>
            {player.role}
          </span>
          <h2>{player.name}</h2>
          <p className="player-club-line">
            <ClubLabel name={player.clubName} logoUrl={player.clubLogoUrl} /> ·
            Offerta iniziale 1 credito
          </p>
          <PlayerValuation player={player} />
        </div>
      </div>
      <div className="call-settings-row">
        <div className="call-settings">
          <form.Field name="duration">
            {(field) => (
              <fieldset className="call-option-field" disabled={disabled}>
                <legend>Timer</legend>
                <div className="call-options">
                  {durations.map((seconds) => (
                    <Button
                      key={seconds}
                      variant="outline"
                      className="call-option"
                      id={
                        field.state.value === seconds
                          ? 'call-duration'
                          : undefined
                      }
                      aria-label={`${seconds} secondi`}
                      aria-pressed={field.state.value === seconds}
                      onClick={() => field.handleChange(seconds)}
                    >
                      {seconds} s
                    </Button>
                  ))}
                </div>
              </fieldset>
            )}
          </form.Field>
          <form.Field name="increments">
            {(field) => (
              <fieldset className="call-option-field" disabled={disabled}>
                <legend>Incrementi dei rilanci</legend>
                <div className="call-options">
                  {incrementOptions.map((increment) => {
                    const selected = field.state.value.includes(increment)
                    const lastSelected =
                      selected && field.state.value.length === 1
                    return (
                      <Button
                        key={increment}
                        variant="outline"
                        className="call-option"
                        aria-pressed={selected}
                        aria-disabled={lastSelected || undefined}
                        onClick={() => {
                          if (lastSelected) return
                          field.handleChange(
                            selected
                              ? field.state.value.filter(
                                  (value) => value !== increment,
                                )
                              : [...field.state.value, increment].sort(
                                  (a, b) => a - b,
                                ),
                          )
                        }}
                      >
                        +{increment}
                      </Button>
                    )
                  })}
                </div>
                {field.state.meta.errors[0] && (
                  <p className="field-error" role="alert">
                    {field.state.meta.errors[0].message}
                  </p>
                )}
              </fieldset>
            )}
          </form.Field>
        </div>
        <div className="call-actions">
          <Button variant="ghost" onClick={onCancel} disabled={disabled}>
            Annulla
          </Button>
          <form.Subscribe selector={(state) => state.isSubmitting}>
            {(busy) => (
              <Button type="submit" disabled={disabled || busy}>
                Chiama a 1 credito
              </Button>
            )}
          </form.Subscribe>
        </div>
      </div>
    </form>
  )
}
