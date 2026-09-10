import { PlayerValuation } from './player-valuation'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import type { CatalogEntry } from '../types/auction.types'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'

const schema = z.object({
  duration: z
    .number()
    .refine(
      (value) => [5, 10, 15, 20, 25, 30].includes(value),
      'Scegli una durata valida.',
    ),
  increments: z.string().refine((value) => {
    const items = value.split(',').map(Number)
    return (
      items.length >= 1 &&
      items.length <= 10 &&
      items.every(
        (item) => Number.isSafeInteger(item) && item > 0 && item <= 1_000_000,
      ) &&
      new Set(items).size === items.length
    )
  }, 'Inserisci da 1 a 10 incrementi interi positivi diversi, separati da virgole.'),
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
    defaultValues: { duration: 15, increments: '1, 5, 10' },
    validators: { onSubmit: schema },
    onSubmit: async ({ value }) => {
      if (!disabled)
        await onStart(value.duration, value.increments.split(',').map(Number))
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
        <span className="auction-preview-badge">Anteprima privata</span>
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
              <div className="form-field">
                <label htmlFor="call-duration">Timer</label>
                <select
                  id="call-duration"
                  value={field.state.value}
                  onChange={(event) =>
                    field.handleChange(Number(event.target.value))
                  }
                  disabled={disabled}
                >
                  {[5, 10, 15, 20, 25, 30].map((seconds) => (
                    <option key={seconds} value={seconds}>
                      {seconds} secondi
                    </option>
                  ))}
                </select>
              </div>
            )}
          </form.Field>
          <form.Field name="increments">
            {(field) => (
              <div className="form-field">
                <label htmlFor="call-increments">Incrementi dei rilanci</label>
                <Input
                  id="call-increments"
                  value={field.state.value}
                  onChange={(event) => field.handleChange(event.target.value)}
                  onBlur={field.handleBlur}
                  disabled={disabled}
                  aria-invalid={field.state.meta.errors.length > 0}
                  aria-describedby={
                    field.state.meta.errors.length
                      ? 'increments-error'
                      : undefined
                  }
                />
                {field.state.meta.errors[0] && (
                  <p className="field-error" id="increments-error" role="alert">
                    {field.state.meta.errors[0].message}
                  </p>
                )}
              </div>
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
