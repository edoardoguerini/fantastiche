import { useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import { Icon } from '@/components/common/icon'
import { ApiError, errorMessage } from '@/lib/api/error'
import { initials } from '@/lib/utils/initials'
import { acceptInvitation } from '../actions/invitation.commands'
import {
  acceptanceFormSchema,
  passwordRules,
  passwordRuleStatus,
} from '../validations/invitation.validations'
import type { Acceptance, InvitationPreview } from '../types/invitation.types'

function submitLabel(invitation: InvitationPreview) {
  if (invitation.requiresLogin)
    return invitation.requiresTeam
      ? `Entra in ${invitation.leagueName}`
      : 'Accetta invito'
  return invitation.requiresTeam
    ? 'Attiva account e partecipa'
    : 'Attiva account e organizza'
}

export function AcceptInvitationForm({
  token,
  invitation,
  onAccepted,
  onUnavailable,
  onSwitchAccount,
}: {
  token: string
  invitation: InvitationPreview
  onAccepted: (value: Acceptance) => void | Promise<void>
  onUnavailable: () => void
  onSwitchAccount?: () => void
}) {
  const [error, setError] = useState<unknown>(null)
  const [showPassword, setShowPassword] = useState(false)
  const lock = useRef(false)
  const form = useForm({
    defaultValues: { teamName: '', password: '' },
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
  const wrongAccount = error instanceof ApiError && error.status === 403
  const uncertain =
    error instanceof ApiError &&
    (error.status === 0 || error.status >= 500 || error.status === 410)
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
        <fieldset className="invitation-group">
          <legend className="sr-only">La tua squadra</legend>
          <form.Field name="teamName">
            {(field) => {
              const invalid = field.state.meta.errors.length > 0
              const name = field.state.value.trim()
              return (
                <div className="form-field">
                  <label htmlFor="invite-team">Nome squadra</label>
                  <Input
                    id="invite-team"
                    value={field.state.value}
                    autoFocus
                    autoComplete="off"
                    maxLength={100}
                    placeholder="Es. Atletico Divano"
                    onChange={(event) => field.handleChange(event.target.value)}
                    onBlur={field.handleBlur}
                    aria-invalid={invalid}
                    aria-describedby={
                      invalid ? 'invite-team-error' : 'invite-team-preview'
                    }
                  />
                  {invalid ? (
                    <p
                      id="invite-team-error"
                      className="field-error"
                      role="alert"
                    >
                      {field.state.meta.errors[0]?.message}
                    </p>
                  ) : (
                    <p id="invite-team-preview" className="invitation-preview">
                      {name ? (
                        <>
                          <span
                            className="invitation-preview-badge"
                            aria-hidden="true"
                          >
                            {initials(name)}
                          </span>
                          <span>
                            In lega apparirai come <strong>{name}</strong>
                          </span>
                        </>
                      ) : (
                        <span>Potrai cambiarlo fino all’inizio dell’asta.</span>
                      )}
                    </p>
                  )}
                </div>
              )
            }}
          </form.Field>
        </fieldset>
      )}
      {!invitation.requiresLogin && (
        <fieldset className="invitation-group">
          <legend className="sr-only">Il tuo account</legend>
          <form.Field name="password">
            {(field) => {
              const invalid = field.state.meta.errors.length > 0
              const status = passwordRuleStatus(field.state.value)
              return (
                <div className="form-field">
                  <label htmlFor="invite-password">Password</label>
                  <div className="password-input">
                    <Input
                      id="invite-password"
                      type={showPassword ? 'text' : 'password'}
                      autoComplete="new-password"
                      value={field.state.value}
                      onChange={(event) =>
                        field.handleChange(event.target.value)
                      }
                      onBlur={field.handleBlur}
                      aria-invalid={invalid}
                      aria-describedby={
                        invalid
                          ? 'invite-password-error'
                          : 'invite-password-rules'
                      }
                    />
                    <button
                      type="button"
                      className="password-toggle"
                      aria-label={
                        showPassword ? 'Nascondi password' : 'Mostra password'
                      }
                      aria-pressed={showPassword}
                      onClick={() => setShowPassword(!showPassword)}
                    >
                      <Icon name={showPassword ? 'eye-slash' : 'eye'} />
                    </button>
                  </div>
                  {invalid && (
                    <p
                      id="invite-password-error"
                      className="field-error"
                      role="alert"
                    >
                      {field.state.meta.errors[0]?.message}
                    </p>
                  )}
                  <ul
                    id="invite-password-rules"
                    className="password-rules"
                    aria-label="Requisiti della password"
                  >
                    {passwordRules.map((rule) => (
                      <li key={rule.id} data-satisfied={status[rule.id]}>
                        {status[rule.id] ? (
                          <Icon name="check" />
                        ) : (
                          <span className="password-rule-dot" aria-hidden />
                        )}
                        <span>{rule.label}</span>
                        <span className="sr-only">
                          {status[rule.id] ? ': soddisfatto' : ': mancante'}
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              )
            }}
          </form.Field>
        </fieldset>
      )}
      {error !== null && (
        <div className="form-error invitation-form-error" role="alert">
          <p>
            {wrongAccount
              ? 'L’invito è per un altro destinatario. Accedi con l’account a cui è stata inviata l’email.'
              : errorMessage(error)}
          </p>
          {wrongAccount && onSwitchAccount && (
            <Button variant="outline" onClick={onSwitchAccount}>
              Cambia account
            </Button>
          )}
          {uncertain && (
            <>
              <p>
                Verifica lo stato dell’invito prima di riprovare. Se l’account è
                stato attivato, puoi accedere.
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
          <Button
            type="submit"
            className="invitation-submit"
            disabled={busy}
            aria-busy={busy}
          >
            {busy ? 'Adesione in corso…' : submitLabel(invitation)}
          </Button>
        )}
      </form.Subscribe>
    </form>
  )
}
