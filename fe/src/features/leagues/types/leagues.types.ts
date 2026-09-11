import { z } from 'zod'

export const leagueSchema = z.object({
  id: z.string(),
  name: z.string(),
  logoUrl: z.string().nullish(),
  myTeamName: z.string().nullish(),
  auctionStatus: z
    .enum(['NotStarted', 'Active', 'Paused', 'Completed'])
    .nullish(),
  leagueSeasonId: z.string(),
  seasonName: z.string(),
  budget: z.number(),
  goalkeepers: z.number(),
  defenders: z.number(),
  midfielders: z.number(),
  forwards: z.number(),
})
export const leaguePageSchema = z.object({
  items: z.array(leagueSchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
})
export type League = z.infer<typeof leagueSchema>
