import { queryOptions } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import { leaguePageSchema, leagueSchema } from '../types/leagues.types'

export const leaguesQueryOptions = (userId: string, page: number) =>
  queryOptions({
    queryKey: ['leagues', userId, 'list', page],
    staleTime: 0,
    refetchInterval: 15_000,
    queryFn: async ({ signal }) =>
      leaguePageSchema.parse(
        await api.get(`/Leagues?page=${page}&pageSize=20`, signal),
      ),
  })
export const leagueQueryOptions = (userId: string, leagueId: string) =>
  queryOptions({
    queryKey: ['leagues', userId, leagueId, 'detail'],
    queryFn: async ({ signal }) =>
      leagueSchema.parse(
        await api.get(`/Leagues/${encodeURIComponent(leagueId)}`, signal),
      ),
  })
