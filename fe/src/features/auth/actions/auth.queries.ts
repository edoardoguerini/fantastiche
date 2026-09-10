import { queryOptions } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import { ApiError } from '@/lib/api/error'
import { authenticatedUserSchema } from '../types/auth.types'

export const authKey = ['auth', 'me'] as const
export const authQueryOptions = () =>
  queryOptions({
    queryKey: authKey,
    queryFn: async ({ signal }) => {
      try {
        return authenticatedUserSchema.parse(await api.get('/Auth/Me', signal))
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) return null
        throw error
      }
    },
    staleTime: 0,
    retry: false,
    refetchOnWindowFocus: 'always',
    refetchInterval: 60_000,
  })
