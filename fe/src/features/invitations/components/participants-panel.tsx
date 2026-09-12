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
  return (
    <>
      <section
        className="league-main participants-panel"
        aria-labelledby="participants-title"
      >
        <header className="participants-heading">
          <h2 id="participants-title">
            Partecipanti <span>{participants.length}</span>
          </h2>
          <p>Ognuno sceglie il nome della propria squadra all’ingresso.</p>
        </header>
        {!participants.length ? (
          <p className="invitation-hint">
            I partecipanti appariranno qui dopo aver accettato.
          </p>
        ) : (
          <ul className="participant-list">
            {participants.map((member) => {
              const title = member.teamName ?? member.displayName
              return (
                <li key={member.userId}>
                  <span className="participant-avatar" aria-hidden="true">
                    {initials(title)}
                  </span>
                  <div>
                    <strong>{title}</strong>
                    <p>
                      {member.teamName
                        ? member.displayName
                        : 'Organizzatore senza squadra'}
                      {member.isOrganizer && member.teamName && (
                        <span className="participant-role-inline">
                          {' '}
                          · Organizzatore
                        </span>
                      )}
                    </p>
                  </div>
                  {member.isOrganizer && (
                    <span className="participant-badge">
                      <Icon name="trophy" />
                      Organizzatore
                    </span>
                  )}
                  <span className="participant-status">Attivo</span>
                </li>
              )
            })}
          </ul>
        )}
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
                : 'Non ci sono ancora inviti per questa stagione. Quando ne invii uno resta qui, così puoi reinviarlo o revocarlo.'}
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
