import { queryOptions } from '@tanstack/react-query'
import { createIsomorphicFn } from '@tanstack/react-start'
import { api } from '@/lib/api/client'
import { ApiError } from '@/lib/api/error'
import { authenticatedUserSchema } from '../types/auth.types'

const getSession = createIsomorphicFn()
  .server(async (signal?: AbortSignal) => {
    const { resolveServerSession } = await import('./auth.server')
    return resolveServerSession(signal)
  })
  .client(async (signal?: AbortSignal) => {
    try {
      return authenticatedUserSchema.parse(await api.get('/Auth/Me', signal))
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return null
      throw error
    }
  })

export const authKey = ['auth', 'me'] as const
export const authQueryOptions = () =>
  queryOptions({
    queryKey: authKey,
    queryFn: ({ signal }) => getSession(signal),
    staleTime: 0,
    retry: false,
    refetchOnWindowFocus: 'always',
    refetchInterval: 60_000,
  })
