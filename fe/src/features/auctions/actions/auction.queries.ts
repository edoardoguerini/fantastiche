import { queryOptions } from '@tanstack/react-query'
import { keepLatestSession } from '../validations/auction-rules'
import { api } from '@/lib/api/client'
import {
  roomSchema,
  sessionSchema,
  pageSchema,
  catalogEntrySchema,
  rosterEntrySchema,
  bidSchema,
  type TimedSession,
} from '../types/auction.types'

export const auctionKeys = {
  all: (userId: string) => ['auctions', userId] as const,
  room: (userId: string, leagueId: string) =>
    ['auctions', userId, 'room', leagueId] as const,
  session: (userId: string, sessionId: string) =>
    ['auctions', userId, 'session', sessionId] as const,
}
export const roomQueryOptions = (
  userId: string,
  leagueId: string,
  seasonId: string,
) =>
  queryOptions({
    queryKey: auctionKeys.room(userId, leagueId),
    queryFn: async ({ signal }) =>
      roomSchema.parse(
        await api.get(
          `/Leagues/${leagueId}/Seasons/${seasonId}/AuctionRoom`,
          signal,
        ),
      ),
    refetchInterval: 10_000,
  })
export const sessionQueryOptions = (userId: string, sessionId: string) =>
  queryOptions({
    queryKey: auctionKeys.session(userId, sessionId),
    queryFn: async ({ signal }): Promise<TimedSession> => ({
      ...sessionSchema.parse(
        await api.get(`/Auctions/Sessions/${sessionId}`, signal),
      ),
      receivedAt: performance.now(),
    }),
    structuralSharing: (previous, next) =>
      keepLatestSession(
        previous as TimedSession | undefined,
        next as TimedSession,
      ),
    staleTime: 0,
    refetchInterval: 5000,
  })
export const catalogQueryOptions = (
  userId: string,
  sessionId: string,
  search: string,
  role: string,
  page: number,
) =>
  queryOptions({
    queryKey: ['auctions', userId, 'catalog', sessionId, search, role, page],
    queryFn: async ({ signal }) =>
      pageSchema(catalogEntrySchema).parse(
        await api.get(
          `/Auctions/Sessions/${sessionId}/Catalog?${new URLSearchParams({ search, role, page: String(page), pageSize: '30', availableOnly: 'true' })}`,
          signal,
        ),
      ),
  })
export const rosterQueryOptions = (
  userId: string,
  sessionId: string,
  teamId = '',
  page = 1,
) =>
  queryOptions({
    queryKey: ['auctions', userId, 'roster', sessionId, teamId, page],
    queryFn: async ({ signal }) =>
      pageSchema(rosterEntrySchema).parse(
        await api.get(
          `/Auctions/Sessions/${sessionId}/Roster?${new URLSearchParams({ ...(teamId ? { teamId } : {}), page: String(page), pageSize: '100' })}`,
          signal,
        ),
      ),
  })
export const bidsQueryOptions = (
  userId: string,
  sessionId: string,
  auctionId: string,
) =>
  queryOptions({
    queryKey: ['auctions', userId, 'bids', sessionId, auctionId],
    queryFn: async ({ signal }) =>
      pageSchema(bidSchema).parse(
        await api.get(
          `/Auctions/Sessions/${sessionId}/Players/${auctionId}/Bids?page=1&pageSize=10`,
          signal,
        ),
      ),
    enabled: !!auctionId,
  })
