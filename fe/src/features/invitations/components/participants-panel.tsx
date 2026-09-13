import { useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { InvitationRow } from './invitation-row'
import { errorMessage } from '@/lib/api/error'
import {
  participantsQueryOptions,
  participantKeys,
} from '../actions/invitation.queries'
import { manageInvitation } from '../actions/invitation.commands'
import { InviteParticipantForm } from './invite-participant-form'
import '../invitation.css'

function initials(name: string) {
  const words = name.trim().split(/\s+/).filter(Boolean)
  const letters = words.length > 1 ? [words[0], words.at(-1)] : [words[0]]
  return letters
    .map((word) => word?.[0] ?? '')
    .join('')
    .toLocaleUpperCase('it')
}
// Il pannello restituisce due sezioni sorelle: l'elenco (colonna principale
// della pagina lega) e il form di invito (colonna laterale). La griglia è
// della pagina, che le posiziona con le classi league-main e league-aside.
export function ParticipantsPanel({
  userId,
  leagueId,
  seasonId,
}: {
  userId: string
  leagueId: string
  seasonId: string
}) {
  const [page, setPage] = useState(1)
  const [confirm, setConfirm] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [error, setError] = useState<unknown>(null)
  const lock = useRef(false)
  const client = useQueryClient()
  const result = useQuery(
    participantsQueryOptions(userId, leagueId, seasonId, page),
  )
  const refresh = async () => {
    await client.invalidateQueries({
      queryKey: participantKeys(userId, leagueId, seasonId),
    })
  }
  const manage = async (id: string, action: 'Revoke' | 'Resend') => {
    if (lock.current) return
    lock.current = true
    setBusy(true)
    setError(null)
    setMessage('')
    try {
      await manageInvitation(leagueId, id, action)
      setMessage(
        action === 'Revoke'
          ? 'Invito revocato.'
          : 'Nuovo invito accodato. Il collegamento precedente non è più valido.',
      )
      setConfirm(null)
    } catch (failure) {
      setError(failure)
    } finally {
      await refresh()
      setBusy(false)
      lock.current = false
    }
  }
  if (result.isPending)
    return (
      <p className="league-main participants-loading" role="status">
        Verifica partecipanti…
      </p>
    )
  if (result.isError)
    return (
      <div className="league-main participants-loading">
        <p>Partecipanti momentaneamente non disponibili.</p>
        <Button variant="ghost" onClick={() => void result.refetch()}>
          Riprova partecipanti
        </Button>
      </div>
    )
  if (!result.data.canManage) return null
  const { participants, invitations } = result.data
  const activeInvitations = invitations.items.filter(
    (invite) => invite.status === 'Pending' || invite.status === 'Expired',
  )
  const pastInvitations = invitations.items.filter(
    (invite) => invite.status === 'Accepted' || invite.status === 'Revoked',
  )
  const renderInvitation = (invite: (typeof invitations.items)[number]) => (
    <InvitationRow
      key={invite.id}
      invite={invite}
      busy={busy}
      confirming={confirm === invite.id}
      onResend={() => void manage(invite.id, 'Resend')}
      onRevoke={() => setConfirm(invite.id)}
      onCancel={() => setConfirm(null)}
      onConfirm={() => void manage(invite.id, 'Revoke')}
    />
  )
  return (
    <>
      <section
        className="league-main participants-panel"
        aria-labelledby="participants-title"
      >
        <header className="participants-heading">
          <h2 id="participants-title">
            Partecipanti{' '}
            <span className="people-count">{participants.length}</span>
          </h2>
        </header>
        {!participants.length ? (
          <p className="invitation-hint">
            Nessun partecipante ancora. Apparirà qui chi accetta l’invito.
          </p>
        ) : (
          <ul className="participant-list">
            {participants.map((member) => {
              const title = member.displayName
              return (
                <li key={member.userId} className="people-row">
                  <span className="participant-avatar" aria-hidden="true">
                    {initials(title)}
                  </span>
                  <div className="people-identity">
                    <strong>{title}</strong>
                    <p>{member.teamName ?? 'Nessuna squadra'}</p>
                  </div>
                  <div className="people-badges">
                    <span
                      className={`people-badge ${member.isOrganizer ? 'people-badge--organizer' : ''}`}
                    >
                      {member.isOrganizer ? 'Organizzatore' : 'Partecipante'}
                    </span>
                    <span className="people-badge people-badge--active">
                      Attivo
                    </span>
                  </div>
                </li>
              )
            })}
          </ul>
        )}
        <section
          className="invitations-section"
          aria-labelledby="invitations-title"
        >
          <header className="participants-heading">
            <h2 id="invitations-title">
              Inviti{' '}
              <span className="people-count">{invitations.totalCount}</span>
            </h2>
          </header>
          {message && (
            <p className="invitation-success" role="status">
              {message}
            </p>
          )}
          {error !== null && (
            <p className="form-error" role="alert">
              {errorMessage(error)} Controlla lo stato aggiornato prima di
              riprovare.
            </p>
          )}
          {activeInvitations.length ? (
            <ul className="invitation-list" aria-label="Inviti da gestire">
              {activeInvitations.map(renderInvitation)}
            </ul>
          ) : (
            <p className="invitation-hint">
              {invitations.totalCount === 0
                ? 'Non hai ancora inviato inviti.'
                : invitations.totalCount > invitations.pageSize
                  ? 'Nessun invito da gestire in questa pagina.'
                  : 'Nessun invito da gestire.'}
            </p>
          )}
          {pastInvitations.length > 0 && (
            <details className="invitation-history" key={page}>
              <summary>
                <Icon name="chevron-right" />
                <span>Storico inviti</span>
                <span className="people-count">{pastInvitations.length}</span>
              </summary>
              {invitations.totalCount > invitations.pageSize && (
                <p className="invitation-hint">
                  Inviti conclusi in questa pagina.
                </p>
              )}
              <ul className="invitation-list" aria-label="Inviti conclusi">
                {pastInvitations.map(renderInvitation)}
              </ul>
            </details>
          )}
          {(page > 1 || invitations.totalCount > invitations.pageSize) && (
            <nav
              className="invitation-pagination"
              aria-label="Pagine degli inviti"
            >
              <Button
                variant="ghost"
                disabled={page === 1 || busy}
                onClick={() => setPage(page - 1)}
              >
                Precedente
              </Button>
              <span>Pagina {page}</span>
              <Button
                variant="ghost"
                disabled={
                  page * invitations.pageSize >= invitations.totalCount || busy
                }
                onClick={() => setPage(page + 1)}
              >
                Successiva
              </Button>
            </nav>
          )}
        </section>
      </section>
      <section
        className="league-aside league-panel invite-panel"
        aria-labelledby="invite-title"
      >
        <h2 id="invite-title">Invita un partecipante</h2>
        <p>
          Riceve un’email con il link per entrare e scegliere il nome della
          squadra.
        </p>
        <InviteParticipantForm
          leagueId={leagueId}
          seasonId={seasonId}
          onUpdated={async () => {
            setPage(1)
            await refresh()
          }}
        />
      </section>
    </>
  )
}
