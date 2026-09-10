import { useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import { errorMessage } from '@/lib/api/error'
import { inviteParticipant } from '../actions/invitation.commands'
import { inviteSchema } from '../validations/invitation.validations'

export function InviteParticipantForm({
  leagueId,
  seasonId,
  onUpdated,
}: {
  leagueId: string
  seasonId: string
  onUpdated: () => Promise<void>
}) {
  const [error, setError] = useState<unknown>(null)
  const [sent, setSent] = useState(false)
  const lock = useRef(false)
  const form = useForm({
    defaultValues: { displayName: '', email: '' },
    validators: { onSubmit: inviteSchema },
    onSubmit: async ({ value }) => {
      if (lock.current) return
      lock.current = true
      setError(null)
      setSent(false)
      try {
        await inviteParticipant(
          leagueId,
          seasonId,
          value.displayName.trim(),
          value.email.trim(),
        )
        setSent(true)
        form.reset()
      } catch (failure) {
        setError(failure)
      } finally {
        await onUpdated()
        lock.current = false
      }
    },
  })
  return (
    <form
      className="invite-participant-form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        void form.handleSubmit()
      }}
    >
      <div className="invite-participant-fields">
        {(['displayName', 'email'] as const).map((name) => (
          <form.Field key={name} name={name}>
            {(field) => (
              <div className="form-field">
                <label htmlFor={`participant-${name}`}>
                  {name === 'displayName'
                    ? 'Nome partecipante'
                    : 'Email partecipante'}
                </label>
                <Input
                  id={`participant-${name}`}
                  type={name === 'email' ? 'email' : 'text'}
                  autoComplete="off"
                  value={field.state.value}
                  onChange={(event) => field.handleChange(event.target.value)}
                  onBlur={field.handleBlur}
                  aria-invalid={field.state.meta.errors.length > 0}
                  aria-describedby={
                    field.state.meta.errors.length
                      ? `participant-${name}-error`
                      : undefined
                  }
                />
                {field.state.meta.errors[0] && (
                  <p
                    className="field-error"
                    id={`participant-${name}-error`}
                    role="alert"
                  >
                    {field.state.meta.errors[0].message}
                  </p>
                )}
              </div>
            )}
          </form.Field>
        ))}
      </div>
      {error !== null && (
        <p className="form-error" role="alert">
          {errorMessage(error)} Controlla l’elenco aggiornato prima di inviare
          di nuovo.
        </p>
      )}
      {sent && (
        <p className="invitation-success" role="status">
          Invito accodato. Il partecipante riceverà il link via email.
        </p>
      )}
      <form.Subscribe selector={(state) => state.isSubmitting}>
        {(busy) => (
          <Button type="submit" disabled={busy} aria-busy={busy}>
            {busy ? 'Invio in corso…' : 'Invia invito'}
          </Button>
        )}
      </form.Subscribe>
    </form>
  )
}
