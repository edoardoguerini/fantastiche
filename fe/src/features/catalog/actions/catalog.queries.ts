import { queryOptions } from '@tanstack/react-query'
import { z } from 'zod'
import { api } from '@/lib/api/client'
import {
  catalogEntrySchema,
  catalogPageSchema,
  listVersionSchema,
  type CatalogFilters,
} from '../types/catalog.types'

export const catalogKeys = {
  all: (userId: string) => ['catalog', userId] as const,
  versions: (userId: string) => ['catalog', userId, 'versions'] as const,
  league: (userId: string, leagueId: string, seasonId: string) =>
    ['catalog', userId, 'league', leagueId, seasonId] as const,
}
export const catalogVersionsOptions = (
  userId: string,
  seasonName: string,
  page: number,
) =>
  queryOptions({
    queryKey: [...catalogKeys.versions(userId), seasonName, page],
    queryFn: async ({ signal }) =>
      catalogPageSchema(listVersionSchema).parse(
        await api.get(
          `/Catalog/Versions?${new URLSearchParams({ seasonName, page: String(page), pageSize: '10' })}`,
          signal,
        ),
      ),
  })
export const catalogEntriesOptions = (
  userId: string,
  versionId: string,
  filters: CatalogFilters,
  page: number,
) =>
  queryOptions({
    queryKey: ['catalog', userId, 'entries', versionId, filters, page],
    queryFn: async ({ signal }) =>
      catalogPageSchema(catalogEntrySchema).parse(
        await api.get(
          `/Catalog/Versions/${versionId}/Entries?${new URLSearchParams({ ...filters, page: String(page), pageSize: '30' })}`,
          signal,
        ),
      ),
  })
export const leagueCatalogPath = (leagueId: string, seasonId: string) =>
  `/Leagues/${leagueId}/Seasons/${seasonId}/Catalog`
export const leagueCatalogOptions = (
  userId: string,
  leagueId: string,
  seasonId: string,
) =>
  queryOptions({
    queryKey: catalogKeys.league(userId, leagueId, seasonId),
    queryFn: async ({ signal }) =>
      listVersionSchema
        .nullable()
        .parse(await api.get(leagueCatalogPath(leagueId, seasonId), signal)),
  })
export const leagueCatalogPermissionOptions = (
  userId: string,
  leagueId: string,
  seasonId: string,
) =>
  queryOptions({
    queryKey: ['catalog', userId, 'permission', leagueId, seasonId],
    queryFn: async ({ signal }) =>
      z
        .object({ canManage: z.boolean() })
        .parse(
          await api.get(
            `/Leagues/${leagueId}/Seasons/${seasonId}/AuctionRoom`,
            signal,
          ),
        ),
    staleTime: 0,
  })
