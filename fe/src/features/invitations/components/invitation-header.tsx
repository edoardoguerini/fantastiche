import { useState } from 'react'
import { Icon } from '@/components/common/icon'
import { initials } from '@/lib/utils/initials'
import { formatExpiry } from '../utils/invitation-copy'
import type { InvitationPreview } from '../types/invitation.types'

function intro(invitation: InvitationPreview) {
  if (invitation.requiresTeam)
    return invitation.requiresLogin
      ? 'ti ha invitato a giocare. Scegli il nome della tua squadra per entrare in lega.'
      : 'ti ha invitato a giocare. Scegli il nome della tua squadra e attiva il tuo account.'
  return invitation.requiresLogin
    ? 'ti ha affidato l’organizzazione della lega.'
    : 'ti ha affidato l’organizzazione della lega. Attiva il tuo account per iniziare.'
}

// Carta della lega in testa alla pagina invito: chi invita, ruolo e scadenza.
export function InvitationHeader({
  invitation,
}: {
  invitation: InvitationPreview
}) {
  const [logoFailed, setLogoFailed] = useState(false)
  const expiry = formatExpiry(invitation.expiresAt)
  return (
    <header className="invitation-league">
      {invitation.leagueLogoUrl && !logoFailed ? (
        <img
          className="invitation-logo"
          src={invitation.leagueLogoUrl}
          alt=""
          role="presentation"
          width="64"
          height="64"
          decoding="async"
          onError={() => setLogoFailed(true)}
        />
      ) : (
        <span className="invitation-logo" aria-hidden="true">
          {initials(invitation.leagueName)}
        </span>
      )}
      <h1 id="invitation-title">{invitation.leagueName}</h1>
      <div className="invitation-meta">
        <span className="invitation-chip">
          <Icon name={invitation.requiresTeam ? 'futbol' : 'trophy'} />
          {invitation.requiresTeam ? 'Partecipante' : 'Organizzatore'}
        </span>
        {expiry && <span className="invitation-expiry">Scade {expiry}</span>}
      </div>
      <p className="invitation-intro">
        <strong>{invitation.invitedBy}</strong> {intro(invitation)}
      </p>
    </header>
  )
}
