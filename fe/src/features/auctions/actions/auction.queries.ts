import { infiniteQueryOptions, queryOptions } from '@tanstack/react-query'
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
  sort = 'name',
) =>
  infiniteQueryOptions({
    queryKey: [
      'auctions',
      userId,
      'catalog',
      sessionId,
      search,
      role,
      sort,
      'infinite',
    ],
    initialPageParam: 1,
    getNextPageParam: nextPage,
    queryFn: async ({ signal, pageParam }) =>
      pageSchema(catalogEntrySchema).parse(
        await api.get(
          `/Auctions/Sessions/${sessionId}/Catalog?${new URLSearchParams({ search, role, page: String(pageParam), pageSize: '30', availableOnly: 'true', sort })}`,
          signal,
        ),
      ),
  })
export const rosterQueryOptions = (
  userId: string,
  sessionId: string,
  teamId = '',
) =>
  infiniteQueryOptions({
    queryKey: ['auctions', userId, 'roster', sessionId, teamId, 'infinite'],
    initialPageParam: 1,
    getNextPageParam: nextPage,
    queryFn: async ({ signal, pageParam }) =>
      pageSchema(rosterEntrySchema).parse(
        await api.get(
          `/Auctions/Sessions/${sessionId}/Roster?${new URLSearchParams({ ...(teamId ? { teamId } : {}), page: String(pageParam), pageSize: '100' })}`,
          signal,
        ),
      ),
  })

function nextPage(last: {
  items: unknown[]
  page: number
  pageSize: number
  total: number
}) {
  return last.items.length > 0 && last.page * last.pageSize < last.total
    ? last.page + 1
    : undefined
}

export function loadedPlayers<T extends { playerId: string }>(
  pages: { items: T[] }[] | undefined,
): T[] {
  const players = new Map<string, T>()
  for (const page of pages ?? [])
    for (const player of page.items) {
      if (!players.has(player.playerId)) players.set(player.playerId, player)
    }
  return [...players.values()]
}
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
