import { useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { errorMessage } from '@/lib/api/error'
import {
  participantsQueryOptions,
  participantKeys,
} from '../actions/invitation.queries'
import { manageInvitation } from '../actions/invitation.commands'
import { InviteParticipantForm } from './invite-participant-form'
import '../invitation.css'

const statusLabels = {
  Pending: 'In attesa',
  Accepted: 'Accettato',
  Expired: 'Scaduto',
  Revoked: 'Revocato',
}
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
      <p className="participants-loading" role="status">
        Verifica partecipanti…
      </p>
    )
  if (result.isError)
    return (
      <div className="participants-loading">
        <p>Partecipanti momentaneamente non disponibili.</p>
        <Button variant="ghost" onClick={() => void result.refetch()}>
          Riprova partecipanti
        </Button>
      </div>
    )
  if (!result.data.canManage) return null
  const { participants, invitations } = result.data
  return (
    <section
      className="participants-panel"
      aria-labelledby="participants-title"
    >
      <header>
        <p className="invitation-eyebrow">LA TUA LEGA</p>
        <h2 id="participants-title">Partecipanti e inviti</h2>
        <p>
          Invita chi giocherà con te. Ognuno sceglierà il nome della propria
          squadra.
        </p>
      </header>
      <div className="participants-grid">
        <div>
          <h3>Invita un partecipante</h3>
          <InviteParticipantForm
            leagueId={leagueId}
            seasonId={seasonId}
            onUpdated={async () => {
              setPage(1)
              await refresh()
            }}
          />
        </div>
        <div>
          <h3>
            In squadra <span>{participants.length}</span>
          </h3>
          {!participants.length ? (
            <p className="invitation-hint">
              I partecipanti appariranno qui dopo aver accettato.
            </p>
          ) : (
            <ul className="participant-list">
              {participants.map((member) => (
                <li key={member.userId}>
                  <Icon name={member.isOrganizer ? 'trophy' : 'futbol'} />
                  <div>
                    <strong>{member.displayName}</strong>
                    <p>
                      {member.teamName ?? 'Organizzatore senza squadra'}
                      {member.isOrganizer && member.teamName
                        ? ' · Organizzatore'
                        : ''}
                    </p>
                  </div>
                  <span>Attivo</span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
      <div className="invitation-history">
        <h3>
          Inviti <span>{invitations.totalCount}</span>
        </h3>
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
        {!invitations.items.length ? (
          <p className="invitation-hint">
            {page > 1
              ? 'Nessun invito in questa pagina.'
              : 'Non ci sono ancora inviti per questa stagione.'}
          </p>
        ) : (
          <ul className="invitation-list">
            {invitations.items.map((invite) => (
              <li key={invite.id}>
                <div className="invitation-recipient">
                  <strong>{invite.displayName}</strong>
                  <p>{invite.email}</p>
                  <small>
                    {invite.kind === 'Organizer'
                      ? 'Organizzatore'
                      : 'Partecipante'}{' '}
                    · Scadenza{' '}
                    {new Date(invite.expiresAt).toLocaleDateString('it-IT')}
                  </small>
                </div>
                <span
                  className={`invitation-status invitation-status--${invite.status.toLowerCase()}`}
                >
                  {statusLabels[invite.status]}
                </span>
                {(invite.status === 'Pending' ||
                  invite.status === 'Expired') && (
                  <div className="invitation-actions">
                    <Button
                      variant="outline"
                      disabled={busy}
                      onClick={() => void manage(invite.id, 'Resend')}
                    >
                      Reinvia
                    </Button>
                    <Button
                      variant="ghost"
                      disabled={busy}
                      onClick={() => setConfirm(invite.id)}
                    >
                      Revoca
                    </Button>
                  </div>
                )}
                {confirm === invite.id && (
                  <div className="invitation-revoke" role="alert">
                    <p>
                      Revocare questo invito? Il link non permetterà più di
                      entrare nella lega.
                    </p>
                    <div className="invitation-actions">
                      <Button
                        variant="ghost"
                        disabled={busy}
                        onClick={() => setConfirm(null)}
                      >
                        Annulla
                      </Button>
                      <Button
                        disabled={busy}
                        onClick={() => void manage(invite.id, 'Revoke')}
                      >
                        Conferma revoca
                      </Button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
        {(page > 1 || invitations.totalCount > 20) && (
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
              disabled={page * 20 >= invitations.totalCount || busy}
              onClick={() => setPage(page + 1)}
            >
              Successiva
            </Button>
          </nav>
        )}
      </div>
    </section>
  )
}
