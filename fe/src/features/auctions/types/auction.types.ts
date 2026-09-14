import { z } from 'zod'

export const auctionParticipantSchema = z.object({
  userId: z.string(),
  displayName: z.string(),
  teamName: z.string().nullable(),
  isOrganizer: z.boolean(),
})
export type AuctionParticipant = z.infer<typeof auctionParticipantSchema>
export const auctionPresenceSchema = z.object({
  sessionId: z.string(),
  connectedUsers: z.number().int().nonnegative(),
  users: z.array(auctionParticipantSchema).optional(),
})

export const teamSchema = z.object({
  id: z.string(),
  name: z.string(),
  budget: z.number(),
  goalkeepers: z.number(),
  defenders: z.number(),
  midfielders: z.number(),
  forwards: z.number(),
})
export const roomSchema = z.object({
  leagueId: z.string(),
  leagueSeasonId: z.string(),
  myTeamId: z.string().nullable(),
  canManage: z.boolean(),
  sessionId: z.string().nullable(),
  listVersionId: z.string().nullable(),
  teams: z.array(teamSchema),
  participants: z.array(auctionParticipantSchema).default([]),
})
export const playerAuctionSchema = z.object({
  currentQuotation: z.number().int().nonnegative().nullable().optional(),
  initialQuotation: z.number().int().nonnegative().nullable().optional(),
  fvm: z.number().int().nonnegative().nullable().optional(),

  birthDate: z.string().nullable().optional(),
  nationality: z.string().nullable().optional(),
  preferredFoot: z.string().nullable().optional(),
  clubLogoUrl: z.string().nullable().optional(),
  photoUrl: z.string().nullable().optional(),
  id: z.string(),
  playerId: z.string(),
  name: z.string(),
  role: z.enum(['P', 'D', 'C', 'A']),
  clubName: z.string(),
  callerTeamId: z.string(),
  winningTeamId: z.string(),
  currentAmount: z.number(),
  durationSeconds: z.number(),
  increments: z.array(z.number()),
  deadline: z.string(),
  status: z.enum(['Open', 'Closed']),
  startedAt: z.string(),
  closedAt: z.string().nullable(),
})
export const bombAuctionSchema = z.object({
  id: z.string(),
  playerId: z.string(),
  name: z.string(),
  role: z.enum(['P', 'D', 'C', 'A']),
  clubName: z.string(),
  photoUrl: z.string().nullable(),
  clubLogoUrl: z.string().nullable(),
  callerTeamId: z.string(),
  status: z.enum([
    'Waiting',
    'Collecting',
    'Revealing',
    'Completed',
    'Cancelled',
    'NoSale',
  ]),
  round: z.number().int().positive(),
  minimumAmount: z.number().int().positive(),
  deadline: z.string(),
  revealStartedAt: z.string().nullable(),
  nextRevealAt: z.string().nullable(),
  participants: z.array(
    z.object({ teamId: z.string(), hasSubmitted: z.boolean() }),
  ),
  revealedOffers: z.array(
    z.object({ teamId: z.string(), amount: z.number().int().positive() }),
  ),
  ownAmount: z.number().int().positive().nullable(),
  playerAuctionId: z.string().nullable(),
  winningTeamId: z.string().nullable(),
  winningAmount: z.number().int().positive().nullable(),
})
export type BombAuctionView = z.infer<typeof bombAuctionSchema>

export const sessionSchema = z.object({
  id: z.string(),
  leagueId: z.string(),
  leagueSeasonId: z.string(),
  listVersionId: z.string(),
  status: z.enum(['Active', 'Paused', 'Completed']),
  version: z.number(),
  currentTeamId: z.string().nullable(),
  currentRole: z.enum(['P', 'D', 'C', 'A']).nullable(),
  teamOrder: z.array(z.string()),
  currentAuction: playerAuctionSchema.nullable(),
  currentBomb: bombAuctionSchema.nullable().optional(),
  usedBombTeamIds: z.array(z.string()).nullable().optional(),
  teams: z.array(teamSchema),
  serverTime: z.string(),
})
export const receiptSchema = z.object({
  requestId: z.string(),
  sessionId: z.string(),
  auctionId: z.string().nullable(),
  version: z.number(),
  serverTime: z.string(),
  accepted: z.boolean(),
  statusCode: z.number(),
  errorCode: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
})
export const catalogEntrySchema = z.object({
  currentQuotation: z.number().int().nonnegative().nullable().optional(),
  initialQuotation: z.number().int().nonnegative().nullable().optional(),
  fvm: z.number().int().nonnegative().nullable().optional(),
  isTransferred: z.boolean().nullable().optional(),
  clubLogoUrl: z.string().nullable().optional(),
  photoUrl: z.string().nullable().optional(),
  playerId: z.string(),
  name: z.string(),
  role: z.enum(['P', 'D', 'C', 'A']),
  clubName: z.string(),
  isAvailable: z.boolean(),
  teamId: z.string().nullable(),
})
export const rosterEntrySchema = z.object({
  clubLogoUrl: z.string().nullable().optional(),
  photoUrl: z.string().nullable().optional(),
  playerId: z.string(),
  teamId: z.string(),
  playerAuctionId: z.string(),
  name: z.string(),
  role: z.enum(['P', 'D', 'C', 'A']),
  clubName: z.string(),
  price: z.number(),
  acquiredAt: z.string(),
})
export const bidSchema = z.object({
  id: z.string(),
  playerAuctionId: z.string(),
  teamId: z.string(),
  userId: z.string(),
  amount: z.number(),
  sequence: z.number(),
  acceptedAt: z.string(),
})
export const pageSchema = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    page: z.number(),
    pageSize: z.number(),
    total: z.number(),
  })
export type AuctionRoom = z.infer<typeof roomSchema>
export type AuctionTeam = z.infer<typeof teamSchema>
export type AuctionSession = z.infer<typeof sessionSchema>
export type TimedSession = AuctionSession & { receivedAt: number }
export type AuctionReceipt = z.infer<typeof receiptSchema>
export type CatalogEntry = z.infer<typeof catalogEntrySchema>
export type RosterEntry = z.infer<typeof rosterEntrySchema>
export type Role = CatalogEntry['role']
export type RosterRules = {
  budget: number
  goalkeepers: number
  defenders: number
  midfielders: number
  forwards: number
}
export const roles = [
  { id: 'P', field: 'goalkeepers', label: 'Portieri' },
  { id: 'D', field: 'defenders', label: 'Difensori' },
  { id: 'C', field: 'midfielders', label: 'Centrocampisti' },
  { id: 'A', field: 'forwards', label: 'Attaccanti' },
] as const
