import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRouter,
  RouterProvider,
} from '@tanstack/react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AccountMenu } from '../account-menu'
import { authQueryOptions } from '../../actions/auth.queries'
import * as pwa from '@/lib/pwa/pwa-store'

const account = {
  id: 'account-1',
  email: 'test@example.test',
  displayName: 'Test Utente',
  isSuperAdmin: false,
}

function state(patch: Partial<pwa.PwaState>) {
  vi.spyOn(pwa, 'getSnapshot').mockReturnValue({
    enabled: true,
    standalone: false,
    canPrompt: false,
    updateReady: false,
    updateDismissed: false,
    installSnoozed: false,
    ...patch,
  })
}

async function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  client.setQueryData(authQueryOptions().queryKey, account)
  const router = createRouter({
    routeTree: createRootRoute({ component: AccountMenu }),
    history: createMemoryHistory({ initialEntries: ['/leghe'] }),
  })
  render(
    <QueryClientProvider client={client}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  const user = userEvent.setup()
  const trigger = await screen.findByRole('button', {
    name: 'Apri menu profilo',
  })
  await waitFor(() => expect(trigger).toBeEnabled())
  await user.click(trigger)
  return { user }
}

afterEach(() => {
  vi.restoreAllMocks()
  pwa.resetPwa()
})

describe('menu account', () => {
  it('offre Installa app e usa il prompt nativo quando c’è', async () => {
    state({ canPrompt: true })
    const prompt = vi.spyOn(pwa, 'promptInstall').mockResolvedValue('accepted')
    const { user } = await setup()
    const items = screen.getAllByRole('menuitem')
    expect(items.map((item) => item.textContent?.trim())).toEqual([
      'Installa app',
      'Esci',
    ])
    expect(items[0]).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    expect(items[1]).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    expect(items[0]).toHaveFocus()
    await user.click(items[0]!)
    expect(prompt).toHaveBeenCalled()
    await waitFor(() => expect(screen.queryByRole('menu')).toBeNull())
  })

  it('senza prompt nativo apre la guida manuale nel menu', async () => {
    state({})
    const { user } = await setup()
    await user.click(screen.getByRole('menuitem', { name: 'Installa app' }))
    expect(screen.getByRole('menu')).toHaveTextContent(
      'Aggiungi alla schermata Home',
    )
  })

  it('nasconde la voce quando l’app è installata o il supporto non è attivo', async () => {
    state({ standalone: true })
    await setup()
    expect(screen.queryByRole('menuitem', { name: 'Installa app' })).toBeNull()
    expect(screen.getByRole('menuitem', { name: 'Esci' })).toHaveFocus()
  })
})
