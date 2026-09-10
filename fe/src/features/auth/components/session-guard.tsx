import { useEffect, type ReactNode } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { authQueryOptions } from '../actions/auth.queries'
import { replaceSession } from '../actions/auth.cache'

export function SessionGuard({ children }: { children: ReactNode }) {
  const session = useQuery(authQueryOptions())
  const client = useQueryClient()
  const navigate = useNavigate()
  useEffect(() => {
    if (session.data === null) {
      void replaceSession(client, null).then(() =>
        navigate({ to: '/login', replace: true }),
      )
    }
  }, [session.data, client, navigate])
  if (session.isError)
    return (
      <ErrorState error={session.error} retry={() => void session.refetch()} />
    )
  if (!session.data) return <LoadingState message="Verifica dell’accesso…" />
  return children
}
