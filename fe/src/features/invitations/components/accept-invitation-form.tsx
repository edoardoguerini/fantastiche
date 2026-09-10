import { useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import { ApiError, errorMessage } from '@/lib/api/error'
import { acceptInvitation } from '../actions/invitation.commands'
import { acceptanceFormSchema } from '../validations/invitation.validations'
import type { Acceptance, InvitationPreview } from '../types/invitation.types'

export function AcceptInvitationForm({
  token,
  invitation,
  onAccepted,
  onUnavailable,
}: {
  token: string
  invitation: InvitationPreview
  onAccepted: (value: Acceptance) => void | Promise<void>
  onUnavailable: () => void
}) {
  const [error, setError] = useState<unknown>(null)
  const lock = useRef(false)
  const form = useForm({
    defaultValues: { teamName: '', password: '', confirmPassword: '' },
    validators: {
      onSubmit: acceptanceFormSchema(
        invitation.requiresTeam,
        !invitation.requiresLogin,
      ),
    },
    onSubmit: async ({ value }) => {
      if (lock.current) return
      lock.current = true
      setError(null)
      try {
        const accepted = await acceptInvitation(
          token,
          invitation.requiresTeam ? value.teamName.trim() : undefined,
          invitation.requiresLogin ? undefined : value.password,
        )
        form.reset()
        await onAccepted(accepted)
      } catch (failure) {
        setError(failure)
      } finally {
        lock.current = false
      }
    },
  })
  return (
    <form
      className="invitation-form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        void form.handleSubmit()
      }}
    >
      {invitation.requiresTeam && (
        <form.Field name="teamName">
          {(field) => (
            <div className="form-field">
              <label htmlFor="invite-team">Nome squadra</label>
              <Input
                id="invite-team"
                value={field.state.value}
                autoComplete="off"
                maxLength={100}
                onChange={(event) => field.handleChange(event.target.value)}
                onBlur={field.handleBlur}
                aria-invalid={field.state.meta.errors.length > 0}
                aria-describedby={
                  field.state.meta.errors.length
                    ? 'invite-team-error'
                    : undefined
                }
              />
              {field.state.meta.errors[0] && (
                <p id="invite-team-error" className="field-error" role="alert">
                  {field.state.meta.errors[0].message}
                </p>
              )}
            </div>
          )}
        </form.Field>
      )}
      {!invitation.requiresLogin && (
        <>
          <p className="invitation-hint">
            Scegli una password di almeno 12 caratteri con maiuscole, minuscole,
            un numero e un simbolo.
          </p>
          {(['password', 'confirmPassword'] as const).map((name) => (
            <form.Field name={name} key={name}>
              {(field) => (
                <div className="form-field">
                  <label htmlFor={`invite-${name}`}>
                    {name === 'password'
                      ? 'Nuova password'
                      : 'Conferma password'}
                  </label>
                  <Input
                    id={`invite-${name}`}
                    type="password"
                    autoComplete="new-password"
                    value={field.state.value}
                    onChange={(event) => field.handleChange(event.target.value)}
                    onBlur={field.handleBlur}
                    aria-invalid={field.state.meta.errors.length > 0}
                    aria-describedby={
                      field.state.meta.errors.length
                        ? `${name}-error`
                        : undefined
                    }
                  />
                  {field.state.meta.errors[0] && (
                    <p
                      id={`${name}-error`}
                      className="field-error"
                      role="alert"
                    >
                      {field.state.meta.errors[0].message}
                    </p>
                  )}
                </div>
              )}
            </form.Field>
          ))}
        </>
      )}
      {error !== null && (
        <div className="form-error" role="alert">
          <p>
            {error instanceof ApiError && error.status === 403
              ? 'Accedi con l’account destinatario dell’invito. Puoi cambiare account qui sotto.'
              : errorMessage(error)}
          </p>
          {error instanceof ApiError &&
            (error.status === 0 ||
              error.status >= 500 ||
              error.status === 410) && (
              <>
                <p>
                  Verifica lo stato dell’invito prima di riprovare. Se l’account
                  è stato attivato, puoi accedere.
                </p>
                <Button variant="outline" onClick={onUnavailable}>
                  Verifica invito
                </Button>
              </>
            )}
        </div>
      )}
      <form.Subscribe selector={(state) => state.isSubmitting}>
        {(busy) => (
          <Button type="submit" disabled={busy} aria-busy={busy}>
            {busy
              ? 'Adesione in corso…'
              : invitation.requiresLogin
                ? 'Accetta invito'
                : invitation.requiresTeam
                  ? 'Attiva account e partecipa'
                  : 'Attiva account e organizza'}
          </Button>
        )}
      </form.Subscribe>
    </form>
  )
}
