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
import { LoadingState } from '@/components/common/page-state'
import { Button } from '@/components/primitives/button'
import { ApiError, errorMessage } from '@/lib/api/error'
import { previewInvitation } from '../actions/invitation.queries'
import { readInvitationToken } from '../validations/invitation.validations'
import type { Acceptance, InvitationPreview } from '../types/invitation.types'
import { AcceptInvitationForm } from './accept-invitation-form'
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
  const [loggingOut, setLoggingOut] = useState(false)
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
    try {
      await logoutMutation.mutateAsync()
      await replaceSession(client, null)
    } catch (failure) {
      setError(failure)
    } finally {
      setLoggingOut(false)
    }
  }
  const unavailable =
    error instanceof ApiError && [404, 410].includes(error.status)
  return (
    <main className="login-page invitation-page" id="main-content">
      <div className="login-container">
        <Brand />
        <section
          className="login-panel invitation-panel"
          aria-labelledby="invitation-title"
        >
          <header className="login-heading">
            <p>IL TUO INVITO</p>
            <h1 id="invitation-title">
              {accepted
                ? 'Benvenuto in lega.'
                : (invitation?.leagueName ?? 'Entriamo in campo.')}
            </h1>
          </header>
          {accepted ? (
            <>
              <p className="invitation-success">
                {session.data ? 'Invito accettato.' : 'Account attivato.'}
              </p>
              <p className="invitation-hint">
                Accedi con l’email a cui hai ricevuto l’invito e la tua password
                per entrare nella lega.
              </p>
              <LoginForm onSuccess={() => enter(accepted)} />
            </>
          ) : !token ? (
            <div className="invitation-message" role="alert">
              <p>Apri il link completo ricevuto via email.</p>
              <p>
                Per proteggere il tuo invito, il collegamento viene rimosso
                dalla barra degli indirizzi: dopo un aggiornamento della pagina
                riaprilo dall’email.
              </p>
            </div>
          ) : error !== null ? (
            <div className="invitation-message" role="alert">
              <p>{errorMessage(error)}</p>
              <p>
                {unavailable
                  ? 'Se hai già accettato, accedi al tuo account. Altrimenti chiedi all’organizzatore un nuovo invito.'
                  : 'Controlla la connessione e riprova.'}
              </p>
              {!unavailable && (
                <Button onClick={retry} variant="outline">
                  Riprova
                </Button>
              )}
            </div>
          ) : !invitation || session.isPending ? (
            <LoadingState message="Verifica dell’invito…" />
          ) : session.isError ? (
            <div className="invitation-message" role="alert">
              <p>{errorMessage(session.error)}</p>
              <Button variant="outline" onClick={() => void session.refetch()}>
                Riprova
              </Button>
            </div>
          ) : (
            <>
              <p className="invitation-hint">
                {invitation.requiresTeam
                  ? 'Scegli il nome della tua squadra e partecipa alla stagione.'
                  : 'Questo invito ti permette di organizzare la lega.'}
              </p>
              <p className="invitation-expiry">
                Valido fino al{' '}
                {new Date(invitation.expiresAt).toLocaleString('it-IT', {
                  dateStyle: 'medium',
                  timeStyle: 'short',
                })}
              </p>
              {invitation.requiresLogin && !session.data ? (
                <>
                  <p className="invitation-hint">
                    Hai già un account: accedi con l’email destinataria
                    dell’invito.
                  </p>
                  <LoginForm onSuccess={() => {}} />
                </>
              ) : !invitation.requiresLogin && session.data ? (
                <p className="invitation-message">
                  Questo invito attiva un nuovo account. Esci dall’account
                  corrente prima di continuare.
                </p>
              ) : (
                <AcceptInvitationForm
                  key={`${session.data?.id ?? 'new'}:${attempt}`}
                  token={token}
                  invitation={invitation}
                  onUnavailable={retry}
                  onAccepted={async (value) => {
                    setToken(null)
                    setAccepted(value)
                    await client.invalidateQueries({ queryKey: ['leagues'] })
                    if (session.data) await enter(value)
                  }}
                />
              )}
              {session.data && (
                <div className="invitation-account">
                  <p>
                    Accesso come <strong>{session.data.email}</strong>
                  </p>
                  <Button
                    variant="ghost"
                    disabled={loggingOut}
                    onClick={() => void logout()}
                  >
                    {loggingOut ? 'Uscita in corso…' : 'Cambia account'}
                  </Button>
                </div>
              )}
            </>
          )}
          <div className="login-invitation">
            <Link to={session.data ? '/leghe' : '/login'} className="back-link">
              {session.data ? 'Vai alle tue leghe' : 'Vai al login'}
            </Link>
          </div>
        </section>
      </div>
    </main>
  )
}
