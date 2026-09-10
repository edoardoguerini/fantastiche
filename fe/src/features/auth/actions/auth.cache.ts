import { QueryCache, QueryClient } from '@tanstack/react-query'
import { ApiError } from '@/lib/api/error'
import { authKey } from './auth.queries'
import type { AuthenticatedUser } from '../types/auth.types'

export function createAuthenticatedQueryClient() {
  const client = new QueryClient({
    queryCache: new QueryCache({
      onError: (error) => {
        if (error instanceof ApiError && error.status === 401)
          client.setQueryData(authKey, null)
      },
    }),
    defaultOptions: {
      queries: { retry: false, staleTime: 30_000 },
      mutations: { retry: false },
    },
  })
  return client
}

export async function replaceSession(
  client: QueryClient,
  user: AuthenticatedUser | null,
) {
  await client.cancelQueries()
  // Mantiene gli observer dell’identità anche quando il login resta nella stessa pagina.
  client.removeQueries({
    predicate: (query) =>
      query.queryKey.length !== authKey.length ||
      query.queryKey.some((part, index) => part !== authKey[index]),
  })
  client.getMutationCache().clear()
  client.setQueryData(authKey, user)
}
