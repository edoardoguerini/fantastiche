import { useEffect, useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useHydrated } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import { errorMessage } from '@/lib/api/error'
import { loginSchema } from '../validations/auth.validations'
import { loginMutationOptions } from '../actions/auth.mutations'
import { replaceSession } from '../actions/auth.cache'

export function LoginForm({
  onSuccess,
  initialEmail = '',
  lockEmail = false,
}: {
  onSuccess: () => void | Promise<void>
  // La pagina invito conosce già l'email del destinatario: la precompila e
  // porta il focus sulla password.
  initialEmail?: string
  lockEmail?: boolean
}) {
  const hydrated = useHydrated()
  const [showPassword, setShowPassword] = useState(false)
  const passwordRef = useRef<HTMLInputElement>(null)
  // Il campo è disabilitato finché non c'è idratazione: il focus va dato dopo.
  useEffect(() => {
    if (hydrated && lockEmail) passwordRef.current?.focus()
  }, [hydrated, lockEmail])
  const [submitError, setSubmitError] = useState<unknown>(null)
  const submitting = useRef(false)
  const client = useQueryClient()
  const login = useMutation(loginMutationOptions())
  const form = useForm({
    defaultValues: { email: initialEmail, password: '' },
    validators: { onSubmit: loginSchema },
    onSubmit: async ({ value }) => {
      if (submitting.current) return
      submitting.current = true
      setSubmitError(null)
      try {
        const user = await login.mutateAsync({
          ...value,
          email: value.email.trim(),
        })
        await replaceSession(client, user)
        await onSuccess()
      } catch (error) {
        setSubmitError(error)
      } finally {
        submitting.current = false
      }
    },
  })

  return (
    <form
      className="login-form"
      method="post"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        event.stopPropagation()
        void form.handleSubmit()
      }}
    >
      <form.Field name="email">
        {(field) => {
          const invalid = field.state.meta.errors.length > 0
          return (
            <div className="form-field">
              <label htmlFor="email">Email</label>
              <Input
                id="email"
                disabled={!hydrated}
                name="email"
                type="email"
                autoComplete="username"
                autoCapitalize="none"
                spellCheck={false}
                readOnly={lockEmail}
                placeholder="nome@esempio.it"
                value={field.state.value}
                onChange={(event) => field.handleChange(event.target.value)}
                onBlur={() => {
                  field.handleChange(field.state.value.trim())
                  field.handleBlur()
                }}
                aria-invalid={invalid}
                aria-describedby={invalid ? 'email-error' : undefined}
              />
              {invalid && (
                <p className="field-error" id="email-error" role="alert">
                  {field.state.meta.errors[0]?.message}
                </p>
              )}
            </div>
          )
        }}
      </form.Field>
      <form.Field name="password">
        {(field) => {
          const invalid = field.state.meta.errors.length > 0
          return (
            <div className="form-field">
              <label htmlFor="password">Password</label>
              <div className="password-input">
                <Input
                  id="password"
                  disabled={!hydrated}
                  name="password"
                  type={showPassword ? 'text' : 'password'}
                  ref={passwordRef}
                  autoComplete="current-password"
                  placeholder="La tua password"
                  value={field.state.value}
                  onChange={(event) => field.handleChange(event.target.value)}
                  onBlur={field.handleBlur}
                  aria-invalid={invalid}
                  aria-describedby={invalid ? 'password-error' : undefined}
                />
                <button
                  type="button"
                  disabled={!hydrated}
                  className="password-toggle"
                  aria-label={
                    showPassword ? 'Nascondi password' : 'Mostra password'
                  }
                  aria-pressed={showPassword}
                  onClick={() => setShowPassword(!showPassword)}
                >
                  {showPassword ? 'Nascondi' : 'Mostra'}
                </button>
              </div>
              {invalid && (
                <p className="field-error" id="password-error" role="alert">
                  {field.state.meta.errors[0]?.message}
                </p>
              )}
            </div>
          )
        }}
      </form.Field>
      {submitError !== null && (
        <p className="form-error" role="alert">
          {errorMessage(submitError)}
        </p>
      )}
      <form.Subscribe selector={(state) => state.isSubmitting}>
        {(isSubmitting) => (
          <Button
            type="submit"
            className="login-submit"
            disabled={!hydrated || isSubmitting}
            aria-busy={isSubmitting}
          >
            {isSubmitting ? 'Accesso in corso…' : 'Accedi'}
          </Button>
        )}
      </form.Subscribe>
    </form>
  )
}
