import { z } from 'zod'

export const listVersionSchema = z.object({
  id: z.string(),
  seasonName: z.string(),
  status: z.enum(['Draft', 'Published']),
  source: z.string(),
  contentHash: z.string(),
  entryCount: z.number(),
  createdAt: z.string(),
  publishedAt: z.string().nullable(),
})
export const catalogEntrySchema = z.object({
  currentQuotation: z.number().int().nonnegative().nullable().optional(),
  initialQuotation: z.number().int().nonnegative().nullable().optional(),
  fvm: z.number().int().nonnegative().nullable().optional(),
  mantraRole: z.string().nullable().optional(),
  currentMantraQuotation: z.number().nullable().optional(),
  initialMantraQuotation: z.number().nullable().optional(),
  mantraFvm: z.number().nullable().optional(),
  isTransferred: z.boolean().nullable().optional(),
  playerId: z.string(),
  externalId: z.string(),
  name: z.string(),
  fullName: z.string(),
  role: z.enum(['P', 'D', 'C', 'A']),
  clubName: z.string(),
  birthDate: z.string(),
  nationality: z.string(),
  preferredFoot: z.string(),
  photoUrl: z.string().nullable().optional(),
  clubLogoUrl: z.string().nullable().optional(),
})
export const catalogPageSchema = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    page: z.number(),
    pageSize: z.number(),
    total: z.number(),
  })
export const leagueCatalogSchema = z.object({
  leagueId: z.string(),
  leagueSeasonId: z.string(),
  listVersionId: z.string(),
})
export type ListVersion = z.infer<typeof listVersionSchema>
export type CatalogFilters = { search: string; role: string; club: string }
