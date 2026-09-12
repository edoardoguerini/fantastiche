import { z } from 'zod'

export const invitationPreviewSchema = z.object({
  leagueName: z.string(),
  leagueLogoUrl: z.string().nullable(),
  invitedBy: z.string(),
  recipientEmailHint: z.string(),
  expiresAt: z.string(),
  requiresLogin: z.boolean(),
  requiresTeam: z.boolean(),
})
export const acceptanceSchema = z.object({
  leagueId: z.string(),
  leagueSeasonId: z.string(),
  teamId: z.string().nullable(),
  email: z.string(),
  teamName: z.string().nullable(),
})
export const invitationDetailsSchema = z.object({
  id: z.string(),
  leagueId: z.string(),
  expiresAt: z.string(),
})
export const participantsSchema = z.object({
  leagueId: z.string(),
  leagueSeasonId: z.string(),
  canManage: z.boolean(),
  participants: z.array(
    z.object({
      userId: z.string(),
      displayName: z.string(),
      teamName: z.string().nullable(),
      isOrganizer: z.boolean(),
    }),
  ),
  invitations: z.object({
    items: z.array(
      z.object({
        id: z.string(),
        displayName: z.string(),
        email: z.string(),
        kind: z.enum(['Organizer', 'Participant']),
        status: z.enum(['Pending', 'Accepted', 'Revoked', 'Expired']),
        expiresAt: z.string(),
      }),
    ),
    totalCount: z.number(),
    page: z.number(),
    pageSize: z.number(),
  }),
})
export type InvitationPreview = z.infer<typeof invitationPreviewSchema>
export type Acceptance = z.infer<typeof acceptanceSchema>
