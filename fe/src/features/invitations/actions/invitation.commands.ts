import { api } from '@/lib/api/client'
import {
  acceptanceSchema,
  invitationDetailsSchema,
} from '../types/invitation.types'

export async function acceptInvitation(
  token: string,
  teamName?: string,
  password?: string,
) {
  return acceptanceSchema.parse(
    await api.post('/Invitations/Accept', {
      token,
      ...(teamName !== undefined ? { teamName } : {}),
      ...(password !== undefined ? { password } : {}),
    }),
  )
}
export async function inviteParticipant(
  leagueId: string,
  leagueSeasonId: string,
  displayName: string,
  email: string,
) {
  return invitationDetailsSchema.parse(
    await api.post(`/Leagues/${leagueId}/Invitations`, {
      leagueSeasonId,
      displayName,
      email,
    }),
  )
}
export async function manageInvitation(
  leagueId: string,
  invitationId: string,
  action: 'Revoke' | 'Resend',
) {
  return api.post(
    `/Leagues/${leagueId}/Invitations/${invitationId}/${action}`,
    {},
  )
}
