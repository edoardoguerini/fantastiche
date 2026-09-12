import { useQuery } from '@tanstack/react-query'
import { participantsQueryOptions } from '../actions/invitation.queries'

function pendingLabel(count: number) {
  if (count === 0) return 'Nessun invito in attesa'
  return count === 1 ? '1 invito in attesa' : `${count} inviti in attesa`
}

// Cella di riepilogo per la pagina lega: condivide la query del pannello
// partecipanti, quindi non aggiunge richieste. Visibile solo a chi gestisce.
export function ParticipantsSummary({
  userId,
  leagueId,
  seasonId,
}: {
  userId: string
  leagueId: string
  seasonId: string
}) {
  const result = useQuery(participantsQueryOptions(userId, leagueId, seasonId))
  if (!result.data?.canManage) return null
  const { participants, invitations } = result.data
  const pending = invitations.items.filter(
    (invite) => invite.status === 'Pending',
  ).length
  return (
    <div className="league-summary-item">
      <dt>Squadre</dt>
      <dd>{participants.length}</dd>
      <p>{pendingLabel(pending)}</p>
    </div>
  )
}
