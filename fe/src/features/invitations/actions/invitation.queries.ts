import { queryOptions } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import {
  invitationPreviewSchema,
  participantsSchema,
} from '../types/invitation.types'

// Il token non entra in query key, cache persistite o URL delle richieste.
export async function previewInvitation(token: string, signal?: AbortSignal) {
  return invitationPreviewSchema.parse(
    await api.get('/Invitations/Preview', signal, {
      'X-Invitation-Token': token,
    }),
  )
}
export const participantKeys = (
  userId: string,
  leagueId: string,
  seasonId: string,
) => ['participants', userId, leagueId, seasonId] as const
export const participantsQueryOptions = (
  userId: string,
  leagueId: string,
  seasonId: string,
  page = 1,
) =>
  queryOptions({
    queryKey: [...participantKeys(userId, leagueId, seasonId), page],
    queryFn: async ({ signal }) =>
      participantsSchema.parse(
        await api.get(
          `/Leagues/${leagueId}/Seasons/${seasonId}/Participants?page=${page}&pageSize=20`,
          signal,
        ),
      ),
    refetchInterval: 30_000,
  })
