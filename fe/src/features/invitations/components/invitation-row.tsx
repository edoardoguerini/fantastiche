import { useEffect, useRef } from 'react'
import { Button } from '@/components/primitives/button'
import type { LeagueInvitation } from '../types/invitation.types'
import { InvitationActionsMenu } from './invitation-actions-menu'

const statusLabels = {
  Pending: 'In attesa',
  Accepted: 'Accettato',
  Expired: 'Scaduto',
  Revoked: 'Revocato',
}

export function InvitationRow({
  invite,
  busy,
  confirming,
  onResend,
  onRevoke,
  onCancel,
  onConfirm,
}: {
  invite: LeagueInvitation
  busy: boolean
  confirming: boolean
  onResend: () => void
  onRevoke: () => void
  onCancel: () => void
  onConfirm: () => void
}) {
  const cancel = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    if (confirming) cancel.current?.focus()
  }, [confirming])
  return (
    <li className="people-row">
      <span className="participant-avatar" aria-hidden="true">
        {invite.displayName.trim().slice(0, 1).toLocaleUpperCase('it')}
      </span>
      <div className="people-identity invitation-recipient">
        <strong>{invite.displayName}</strong>
        <p>{invite.email}</p>
        <small>
          {invite.status === 'Expired' ? 'Scaduto il' : 'Scade il'}{' '}
          {new Date(invite.expiresAt).toLocaleDateString('it-IT')}
        </small>
      </div>
      <div className="people-badges">
        <span
          className={`people-badge ${invite.kind === 'Organizer' ? 'people-badge--organizer' : ''}`}
        >
          {invite.kind === 'Organizer' ? 'Organizzatore' : 'Partecipante'}
        </span>
        <span
          className={`people-badge invitation-status--${invite.status.toLowerCase()}`}
        >
          {statusLabels[invite.status]}
        </span>
      </div>
      {(invite.status === 'Pending' || invite.status === 'Expired') && (
        <InvitationActionsMenu
          name={invite.displayName}
          disabled={busy}
          onResend={onResend}
          onRevoke={onRevoke}
        />
      )}
      {confirming && (
        <div
          className="invitation-revoke"
          role="group"
          aria-label={`Revoca invito ${invite.displayName}`}
        >
          <p>
            Revocare questo invito? Il link non permetterà più di entrare nella
            lega.
          </p>
          <div className="invitation-actions">
            <Button
              ref={cancel}
              variant="ghost"
              disabled={busy}
              onClick={onCancel}
            >
              Annulla
            </Button>
            <Button
              variant="outline"
              className="text-destructive"
              disabled={busy}
              onClick={onConfirm}
            >
              Conferma revoca
            </Button>
          </div>
        </div>
      )}
    </li>
  )
}
