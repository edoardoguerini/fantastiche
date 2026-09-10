import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { ApiError, errorMessage } from '@/lib/api/error'
import { authQueryOptions } from '../actions/auth.queries'
import { logoutMutationOptions } from '../actions/auth.mutations'
import { replaceSession } from '../actions/auth.cache'

export function AccountMenu() {
  const { data: user } = useQuery(authQueryOptions())
  const client = useQueryClient()
  const navigate = useNavigate()
  const logout = useMutation(logoutMutationOptions())
  const [error, setError] = useState<unknown>(null)
  async function signOut() {
    if (logout.isPending) return
    setError(null)
    try {
      await logout.mutateAsync()
    } catch (failure) {
      if (!(failure instanceof ApiError && failure.status === 401)) {
        setError(failure)
        return
      }
    }
    await replaceSession(client, null)
    await navigate({ to: '/login', replace: true })
  }
  return (
    <div className="account-menu">
      <span className="account-name">{user?.displayName}</span>
      <Button
        variant="outline"
        onClick={() => void signOut()}
        disabled={logout.isPending}
      >
        <Icon name="right-from-bracket" />
        {logout.isPending ? 'Uscita…' : 'Esci'}
      </Button>
      {error !== null && (
        <p role="alert" className="field-error">
          {errorMessage(error)}
        </p>
      )}
    </div>
  )
}
