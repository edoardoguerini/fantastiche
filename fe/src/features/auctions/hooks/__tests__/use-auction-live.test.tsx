import { act, renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import type { ReactNode } from 'react'
import { authQueryOptions } from '@/features/auth'
import { useAuctionLive } from '../use-auction-live'
import { sessionQueryOptions } from '../../actions/auction.queries'

const hub = vi.hoisted(() => ({
  state: 'Connected',
  reconnecting: () => {},
  reconnected: () => {},
  invoke: vi.fn(),
}))
vi.mock('@microsoft/signalr', () => ({
  HubConnectionState: { Connected: 'Connected' },
  LogLevel: { None: 0 },
  HubConnectionBuilder: class {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    configureLogging() {
      return this
    }
    build() {
      return {
        get state() {
          return hub.state
        },
        start: async () => {},
        stop: async () => {},
        invoke: hub.invoke,
        on: () => {},
        onclose: () => {},
        onreconnecting: (fn: () => void) => {
          hub.reconnecting = fn
        },
        onreconnected: (fn: () => void) => {
          hub.reconnected = fn
        },
      }
    }
  },
}))
const snapshot = {
  id: 'session',
  leagueId: 'league',
  leagueSeasonId: 'season',
  listVersionId: 'list',
  status: 'Active' as const,
  version: 1,
  currentTeamId: null,
  teamOrder: [],
  currentAuction: null,
  teams: [],
  serverTime: '2026-09-10T10:00:00Z',
}
afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

it('una sincronizzazione precedente non riabilita i rilanci dopo la disconnessione', async () => {
  hub.state = 'Connected'
  hub.invoke.mockResolvedValue(snapshot)
  vi.spyOn(document, 'hidden', 'get').mockReturnValue(false)
  vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(true)
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const user = {
    id: 'user',
    email: 'demo@example.test',
    displayName: 'Demo',
    isSuperAdmin: false,
  }
  client.setQueryData(authQueryOptions().queryKey, user)
  let finish!: () => void
  const refresh = vi.spyOn(client, 'invalidateQueries').mockImplementationOnce(
    () =>
      new Promise<void>((resolve) => {
        finish = resolve
      }),
  )
  const hook = renderHook(() => useAuctionLive('user', 'session'), {
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  })
  await waitFor(() => expect(refresh).toHaveBeenCalled())
  act(() => {
    hub.state = 'Reconnecting'
    hub.reconnecting()
  })
  await act(async () => {
    finish()
  })
  expect(hook.result.current).toBe('reconnecting')
  act(() => {
    hub.state = 'Connected'
    hub.reconnected()
  })
  await waitFor(() => expect(hook.result.current).toBe('online'))
  hook.unmount()
  client.clear()
})

it('una risposta HTTP tardiva non sovrascrive lo stato più recente ricevuto dal canale live', async () => {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  let finish!: (value: Response) => void
  vi.stubGlobal(
    'fetch',
    () =>
      new Promise<Response>((resolve) => {
        finish = resolve
      }),
  )
  const options = sessionQueryOptions('user', snapshot.id)
  const request = client.fetchQuery(options)
  await waitFor(() => expect(finish).toBeTypeOf('function'))
  const newer = { ...snapshot, version: 2, receivedAt: 20 }
  client.setQueryData(options.queryKey, newer)
  finish(
    new Response(
      JSON.stringify({ isSuccess: true, data: snapshot, errors: [] }),
    ),
  )
  await request
  expect(client.getQueryData(options.queryKey)).toEqual(newer)
  client.clear()
})
