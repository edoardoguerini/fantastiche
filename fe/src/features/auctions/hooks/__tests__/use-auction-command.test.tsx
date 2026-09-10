import { act, renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import type { ReactNode } from 'react'
import { authQueryOptions } from '@/features/auth'
import { useAuctionCommand } from '../use-auction-command'
import {
  readPending,
  storePending,
  type PendingCommand,
} from '../../actions/auction.commands'

const user = {
  id: 'user',
  displayName: 'Demo',
  email: 'demo@example.test',
  isSuperAdmin: false,
}
const sid = '00000000-0000-4000-8000-000000000001'
const aid = '00000000-0000-4000-8000-000000000002'
const rid = '00000000-0000-4000-8000-000000000003'
const command: PendingCommand = {
  kind: 'Bids',
  body: { requestId: rid, playerAuctionId: aid, amount: 25 },
}
const receipt = {
  requestId: rid,
  sessionId: sid,
  auctionId: aid,
  version: 2,
  serverTime: '2026-09-10T10:00:00Z',
  accepted: true,
  statusCode: 200,
}
const response = (data: unknown, status = 200) =>
  new Response(
    JSON.stringify({
      isSuccess: status < 400,
      data,
      errors:
        status < 400
          ? []
          : [{ code: 'auth.required', message: 'Accesso richiesto.' }],
    }),
    { status },
  )
function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  client.setQueryData(authQueryOptions().queryKey, user)
  return renderHook(() => useAuctionCommand(user.id, sid, true), {
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  })
}
afterEach(() => {
  vi.unstubAllGlobals()
  sessionStorage.clear()
})

it('recupera la ricevuta dopo riapertura senza reinviare l’offerta', async () => {
  storePending(user.id, sid, command)
  const fetch = vi.fn(async (_url: string) => response(receipt))
  vi.stubGlobal('fetch', fetch)
  const hook = setup()
  await waitFor(() => expect(hook.result.current.pending).toBeNull())
  expect(fetch).toHaveBeenCalledTimes(1)
  expect(String(fetch.mock.calls[0]?.[0])).toContain(`/Commands/${rid}`)
})

it.each([401, 403, 429])(
  'conserva la richiesta incerta se il recupero restituisce %s',
  async (status) => {
    storePending(user.id, sid, command)
    vi.stubGlobal('fetch', async () => response(null, status))
    const hook = setup()
    await waitFor(() => expect(hook.result.current.message).not.toBe(''))
    expect(readPending(user.id, sid)).toEqual(command)
    expect(hook.result.current.pending).toEqual(command)
  },
)

it('reinvia solo su gesto esplicito, conservando UUID e totale originali', async () => {
  storePending(user.id, sid, command)
  const bodies: unknown[] = []
  vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
    if (url.endsWith('/Antiforgery')) return response({ token: 'csrf' })
    if (url.includes('/Commands/')) return response(null, 404)
    bodies.push(JSON.parse(String(init?.body)))
    return response(receipt)
  })
  const hook = setup()
  await waitFor(() =>
    expect(hook.result.current.message).toContain('non ancora disponibile'),
  )
  expect(bodies).toEqual([])
  await act(async () => {
    await hook.result.current.retry()
  })
  expect(bodies).toEqual([command.body])
  expect(hook.result.current.pending).toBeNull()
})

it.each<PendingCommand>([
  { kind: 'Bombs', body: { requestId: rid, playerId: aid } },
  {
    kind: 'BombBids',
    body: { requestId: rid, bombAuctionId: aid, round: 2, amount: 35 },
  },
  { kind: 'CancelBomb', body: { requestId: rid, bombAuctionId: aid } },
])(
  'recupera e reinvia esplicitamente $kind conservando il payload originale',
  async (bombCommand) => {
    storePending(user.id, sid, bombCommand)
    const bodies: unknown[] = []
    const paths: string[] = []
    vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
      if (url.endsWith('/Antiforgery')) return response({ token: 'csrf' })
      if (url.includes('/Commands/')) return response(null, 404)
      paths.push(url)
      bodies.push(JSON.parse(String(init?.body)))
      return response(receipt)
    })
    const hook = setup()
    await waitFor(() =>
      expect(hook.result.current.message).toContain('non ancora disponibile'),
    )
    expect(bodies).toEqual([])
    expect(readPending(user.id, sid)).toEqual(bombCommand)
    await act(async () => {
      await hook.result.current.retry()
    })
    expect(bodies).toEqual([bombCommand.body])
    expect(paths[0]).toContain(`/Sessions/${sid}/${bombCommand.kind}`)
    expect(hook.result.current.pending).toBeNull()
  },
)
