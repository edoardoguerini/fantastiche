import { api } from '@/lib/api/client'
import {
  acceptanceSchema,
  invitationDetailsSchema,
} from '../types/invitation.types'

export async function acceptInvitation(
  token: string,
  teamName?: string,
  password?: string,
  displayName?: string,
) {
  return acceptanceSchema.parse(
    await api.post('/Invitations/Accept', {
      token,
      ...(displayName !== undefined ? { displayName } : {}),
      ...(teamName !== undefined ? { teamName } : {}),
      ...(password !== undefined ? { password } : {}),
    }),
  )
}
export async function inviteParticipant(
  leagueId: string,
  leagueSeasonId: string,
  email: string,
) {
  return invitationDetailsSchema.parse(
    await api.post(`/Leagues/${leagueId}/Invitations`, {
      leagueSeasonId,
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
