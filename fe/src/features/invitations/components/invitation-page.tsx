import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import {
  authQueryOptions,
  LoginForm,
  logoutMutationOptions,
  replaceSession,
} from '@/features/auth'
import { Brand } from '@/components/common/brand'
import { Icon } from '@/components/common/icon'
import { LoadingState } from '@/components/common/page-state'
import { Button } from '@/components/primitives/button'
import { errorMessage } from '@/lib/api/error'
import { previewInvitation } from '../actions/invitation.queries'
import { readInvitationToken } from '../validations/invitation.validations'
import { invitationErrorCopy } from '../utils/invitation-copy'
import type { Acceptance, InvitationPreview } from '../types/invitation.types'
import { AcceptInvitationForm } from './accept-invitation-form'
import { InvitationHeader } from './invitation-header'
import { InvitationSteps } from './invitation-steps'
import '../invitation.css'

export function InvitationPage() {
  const [token, setToken] = useState(() =>
    typeof window === 'undefined'
      ? null
      : readInvitationToken(window.location.href),
  )
  const [invitation, setInvitation] = useState<InvitationPreview | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [attempt, setAttempt] = useState(0)
  const [accepted, setAccepted] = useState<Acceptance | null>(null)
  const [entering, setEntering] = useState(false)
  const [loggingOut, setLoggingOut] = useState(false)
  const [logoutError, setLogoutError] = useState<unknown>(null)
  const session = useQuery(authQueryOptions())
  const logoutMutation = useMutation(logoutMutationOptions())
  const client = useQueryClient()
  const navigate = useNavigate()
  useEffect(() => {
    const url = new URL(window.location.href)
    url.searchParams.delete('token')
    if (new URLSearchParams(url.hash.slice(1)).has('token')) url.hash = ''
    window.history.replaceState(
      window.history.state,
      '',
      url.pathname + url.search + url.hash,
    )
  }, [])
  useEffect(() => {
    if (!token) return
    const controller = new AbortController()
    void previewInvitation(token, controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) {
          setInvitation(value)
          setError(null)
        }
      })
      .catch((failure: unknown) => {
        if (!controller.signal.aborted) {
          setError(failure)
          setInvitation(null)
        }
      })
    return () => controller.abort()
  }, [token, attempt])
  const retry = () => {
    setError(null)
    setInvitation(null)
    setAttempt((value) => value + 1)
  }
  const enter = (value: Acceptance) =>
    navigate({
      to: '/leghe/$leagueId',
      params: { leagueId: value.leagueId },
      replace: true,
    })
  const logout = async () => {
    if (loggingOut) return
    setLoggingOut(true)
    setLogoutError(null)
    try {
      await logoutMutation.mutateAsync()
      await replaceSession(client, null)
    } catch (failure) {
      setLogoutError(failure)
    } finally {
      setLoggingOut(false)
    }
  }
  const signedIn = Boolean(session.data)
  const footer = (
    <div className="invitation-footer">
      {signedIn ? (
        <Link to="/leghe" className="back-link">
          Vai alle tue leghe
        </Link>
      ) : (
        <>
          Hai già un account?{' '}
          <Link to="/login" className="back-link">
            Vai al login
          </Link>
        </>
      )}
    </div>
  )
  const switchAccount = (
    <div className="invitation-switch">
      <Button
        variant="ghost"
        disabled={loggingOut}
        onClick={() => void logout()}
      >
        {loggingOut ? 'Uscita in corso…' : 'Cambia account'}
      </Button>
      {logoutError !== null && (
        <p className="field-error" role="alert">
          {errorMessage(logoutError)}
        </p>
      )}
    </div>
  )
  // Titolo di riserva per gli stati senza carta della lega: la sezione resta
  // nominata per gli screen reader.
  const hiddenTitle = (
    <h1 id="invitation-title" className="sr-only">
      Il tuo invito
    </h1>
  )
  let content: React.ReactNode
  // Negli esiti definitivi l'azione principale sostituisce il piè di pagina.
  let showFooter = true
  if (entering) {
    showFooter = false
    content = (
      <>
        {hiddenTitle}
        <LoadingState message="Entriamo in lega…" />
      </>
    )
  } else if (accepted && invitation) {
    showFooter = false
    content = (
      <>
        <div className="invitation-outcome">
          <span className="invitation-outcome-mark" aria-hidden="true">
            <Icon name="check" />
          </span>
          <h1 id="invitation-title">Sei in {invitation.leagueName}.</h1>
          <p>
            {accepted.teamName ? (
              <>
                <strong>{accepted.teamName}</strong> è pronta.{' '}
              </>
            ) : (
              'Il tuo account è attivo. '
            )}
            Accedi con la password appena scelta per entrare in lega.
          </p>
        </div>
        <LoginForm
          onSuccess={() => enter(accepted)}
          initialEmail={accepted.email}
          lockEmail
        />
      </>
    )
  } else if (!token) {
    content = (
      <div className="invitation-outcome invitation-outcome--error">
        <span className="invitation-outcome-mark" aria-hidden="true">
          <Icon name="clock" />
        </span>
        <h1 id="invitation-title">Apri il link completo.</h1>
        <p role="alert">
          Per proteggere il tuo invito, il collegamento viene rimosso dalla
          barra degli indirizzi: riaprilo dall’email che hai ricevuto.
        </p>
      </div>
    )
  } else if (error !== null) {
    const copy = invitationErrorCopy(error)
    showFooter = copy.retryable
    content = (
      <>
        <div
          className="invitation-outcome invitation-outcome--error"
          role="alert"
        >
          <span className="invitation-outcome-mark" aria-hidden="true">
            <Icon name="clock" />
          </span>
          <h1 id="invitation-title">{copy.title}</h1>
          <p>{copy.hint}</p>
        </div>
        {copy.retryable ? (
          <Button variant="outline" onClick={retry}>
            Riprova
          </Button>
        ) : signedIn ? (
          <Button asChild variant="outline">
            <Link to="/leghe">Vai alle tue leghe</Link>
          </Button>
        ) : (
          <Button asChild variant="outline">
            <Link to="/login">Hai già un account? Accedi</Link>
          </Button>
        )}
      </>
    )
  } else if (!invitation || session.isPending) {
    content = (
      <>
        {hiddenTitle}
        <LoadingState message="Verifica dell’invito…" />
      </>
    )
  } else if (session.isError) {
    content = (
      <>
        {hiddenTitle}
        <div className="invitation-notice" role="alert">
          <p>{errorMessage(session.error)}</p>
          <Button variant="outline" onClick={() => void session.refetch()}>
            Riprova
          </Button>
        </div>
      </>
    )
  } else {
    const stepLabels: [string, string] = [
      'Accedi',
      invitation.requiresTeam ? 'Conferma squadra' : 'Conferma',
    ]
    content = (
      <>
        <InvitationHeader invitation={invitation} />
        {invitation.requiresLogin && !signedIn ? (
          <>
            <InvitationSteps labels={stepLabels} current={1} />
            <p className="invitation-hint">
              Hai già un account Fantastiche. Accedi con{' '}
              <strong>{invitation.recipientEmailHint}</strong> per continuare.
            </p>
            <LoginForm onSuccess={() => {}} />
          </>
        ) : invitation.requiresLogin && session.data ? (
          <>
            <InvitationSteps labels={stepLabels} current={2} />
            <div className="invitation-identity">
              <span className="invitation-avatar" aria-hidden="true">
                {session.data.email.slice(0, 2).toUpperCase()}
              </span>
              <p>
                Stai accettando come <strong>{session.data.email}</strong>
              </p>
              {switchAccount}
            </div>
            <AcceptInvitationForm
              key={`${session.data.id}:${attempt}`}
              token={token}
              invitation={invitation}
              onUnavailable={retry}
              onSwitchAccount={() => void logout()}
              onAccepted={async (value) => {
                // Account già autenticato: si entra in lega senza mostrare
                // né l'esito né un secondo login.
                setToken(null)
                setEntering(true)
                await client.invalidateQueries({ queryKey: ['leagues'] })
                await enter(value)
              }}
            />
          </>
        ) : session.data ? (
          <div className="invitation-notice" role="status">
            <p>
              Questo invito attiva un nuovo account, ma sei collegato come{' '}
              <strong>{session.data.email}</strong>. Esci per continuare.
            </p>
            {switchAccount}
          </div>
        ) : (
          <AcceptInvitationForm
            key={`new:${attempt}`}
            token={token}
            invitation={invitation}
            onUnavailable={retry}
            onAccepted={async (value) => {
              setToken(null)
              setAccepted(value)
              await client.invalidateQueries({ queryKey: ['leagues'] })
            }}
          />
        )}
      </>
    )
  }
  return (
    <main className="login-page invitation-page" id="main-content">
      <div className="login-container invitation-container">
        <Brand />
        <section
          className="login-panel invitation-panel"
          aria-labelledby="invitation-title"
        >
          {content}
          {showFooter && footer}
        </section>
      </div>
    </main>
  )
}
