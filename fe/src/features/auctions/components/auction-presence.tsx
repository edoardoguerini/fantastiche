import { useId } from 'react'
import { Icon } from '@/components/common/icon'
import type { AuctionParticipant } from '../types/auction.types'
import '../auction-presence.css'

export function AuctionPresence({
  connected,
  connectedUsers,
  users,
  participants = [],
  userId,
  awaitingStart = false,
}: {
  connected: boolean
  connectedUsers: number | null
  users: AuctionParticipant[] | null
  participants: AuctionParticipant[]
  userId: string
  awaitingStart?: boolean
}) {
  const panelId = useId()
  const titleId = useId()
  const onlineIds = new Set(users?.map((user) => user.userId))
  const byName = (a: AuctionParticipant, b: AuctionParticipant) =>
    a.displayName.localeCompare(b.displayName, 'it')
  const online = [...(users ?? [])].sort(byName)
  const offline = participants
    .filter((user) => !onlineIds.has(user.userId))
    .sort(byName)

  return (
    <>
      <div role="status" aria-label="Connessione alla sala">
        <button
          type="button"
          className={`auction-live-badge ${connected || awaitingStart ? 'auction-live-badge--online' : ''}`}
          aria-label="Mostra utenti della sala"
          aria-haspopup="dialog"
          popoverTarget={panelId}
        >
          <strong>
            {connected || awaitingStart ? 'LIVE' : 'Riconnessione'}
          </strong>
          {(connected || awaitingStart) && (
            <>
              <span className="auction-presence-label">
                Utenti connessi: {connectedUsers ?? '—'}
              </span>
              <span className="auction-presence-icon">
                <Icon name="user-group" variant="regular" />
                <span className="auction-presence-count" aria-hidden="true">
                  {connectedUsers ?? '—'}
                </span>
              </span>
              {connected && <span className="sr-only">Connesso alla sala</span>}
            </>
          )}
        </button>
      </div>
      <div
        id={panelId}
        popover="auto"
        role="dialog"
        aria-labelledby={titleId}
        className="auction-presence-panel"
      >
        <div className="auction-presence-heading">
          <h2 id={titleId}>Utenti della sala</h2>
          <button
            type="button"
            popoverTarget={panelId}
            popoverTargetAction="hide"
            aria-label="Chiudi utenti della sala"
          >
            <Icon name="xmark" />
          </button>
        </div>
        <p className="auction-presence-description">
          Presenze in questa sessione d’asta.
        </p>
        {awaitingStart ? (
          <p className="auction-presence-message" role="status">
            La sessione non è ancora iniziata. Le presenze saranno aggiornate
            all’avvio dell’asta.
          </p>
        ) : !connected ? (
          <p className="auction-presence-message" role="status">
            Riconnessione in corso. Le presenze verranno aggiornate al
            ripristino.
          </p>
        ) : users === null ? (
          <p className="auction-presence-message" role="status">
            Caricamento presenze…
          </p>
        ) : (
          <>
            <PresenceGroup
              title="Connessi"
              users={online}
              online
              userId={userId}
            />
            <PresenceGroup
              title="Non connessi"
              users={offline}
              userId={userId}
            />
          </>
        )}
      </div>
    </>
  )
}

function PresenceGroup({
  title,
  users,
  online = false,
  userId,
}: {
  title: string
  users: AuctionParticipant[]
  online?: boolean
  userId: string
}) {
  const headingId = useId()
  return (
    <section
      className={`auction-presence-group ${online ? 'auction-presence-group--online' : ''}`}
      aria-labelledby={headingId}
    >
      <h3 id={headingId}>
        {title}
        <span aria-hidden="true">{users.length}</span>
      </h3>
      {users.length ? (
        <ul>
          {users.map((user) => (
            <li key={user.userId}>
              <span className="auction-presence-avatar" aria-hidden="true">
                {user.displayName
                  .split(' ')
                  .filter(Boolean)
                  .slice(0, 2)
                  .map((name) => name[0])
                  .join('')}
              </span>
              <div className="auction-presence-user">
                <p>
                  <strong>{user.displayName}</strong>
                  {user.userId === userId && (
                    <span className="auction-presence-you">Tu</span>
                  )}
                </p>
                <span>
                  {[user.teamName, user.isOrganizer ? 'Organizzatore' : null]
                    .filter(Boolean)
                    .join(' · ') || 'Spettatore'}
                </span>
              </div>
              <span className="auction-presence-dot" aria-hidden="true" />
            </li>
          ))}
        </ul>
      ) : (
        <p className="auction-presence-empty">
          {online
            ? 'Nessun utente connesso.'
            : 'Tutti gli utenti della lega sono connessi.'}
        </p>
      )}
    </section>
  )
}
