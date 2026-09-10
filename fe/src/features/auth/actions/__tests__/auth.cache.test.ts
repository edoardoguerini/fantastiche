import { QueryClient, QueryObserver } from '@tanstack/react-query'
import { expect, it } from 'vitest'
import { replaceSession } from '../auth.cache'
import { authKey } from '../auth.queries'
import type { AuthenticatedUser } from '../../types/auth.types'

it('aggiorna gli observer auth già montati e cancella tutti i dati privati al cambio account', async () => {
  const client = new QueryClient()
  client.setQueryData(authKey, null)
  client.setQueryData(['auctions', 'previous'], { private: true })
  const observer = new QueryObserver<AuthenticatedUser | null>(client, {
    queryKey: authKey,
    enabled: false,
  })
  const received: (AuthenticatedUser | null | undefined)[] = []
  const unsubscribe = observer.subscribe((result) => received.push(result.data))
  const user = {
    id: 'new',
    displayName: 'Giulia',
    email: 'test@example.test',
    isSuperAdmin: false,
  }
  await replaceSession(client, user)
  expect(received.at(-1)).toEqual(user)
  expect(client.getQueryData(['auctions', 'previous'])).toBeUndefined()
  await replaceSession(client, null)
  expect(received.at(-1)).toBeNull()
  unsubscribe()
  client.clear()
})
