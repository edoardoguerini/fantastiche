import { afterEach, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { authQueryOptions, type AuthenticatedUser } from '@/features/auth'
import { api } from '@/lib/api/client'
import { LeagueCatalogSummary } from '../league-catalog-summary'

vi.mock('@/lib/api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))
const uid = '00000000-0000-4000-8000-000000000001'
const version = {
  id: '00000000-0000-4000-8000-000000000002',
  seasonName: '2026/27',
  status: 'Published' as const,
  source: 'FantacalcioCsv',
  contentHash: 'abc',
  entryCount: 594,
  createdAt: '2026-09-10T00:24:00Z',
  publishedAt: '2026-09-10T11:00:00Z',
}
function wrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  const authenticated: AuthenticatedUser = {
    id: uid,
    isSuperAdmin: false,
    email: 'demo@example.test',
    displayName: 'Demo',
  }
  client.setQueryData(authQueryOptions().queryKey, () => authenticated)
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>
      <dl>{children}</dl>
    </QueryClientProvider>
  )
}
afterEach(() => vi.resetAllMocks())

it('riassume il listone fissato con numero di calciatori e versione', async () => {
  vi.mocked(api.get).mockResolvedValue(version)
  render(<LeagueCatalogSummary leagueId="league" seasonId="season" />, {
    wrapper: wrapper(),
  })
  expect(await screen.findByText(/Versione del/)).toBeInTheDocument()
  expect(screen.getByRole('definition')).toHaveTextContent('594 calciatori')
})

it('segnala il listone ancora da scegliere', async () => {
  vi.mocked(api.get).mockResolvedValue(null)
  render(<LeagueCatalogSummary leagueId="league" seasonId="season" />, {
    wrapper: wrapper(),
  })
  expect(await screen.findByText('Da scegliere')).toBeInTheDocument()
  expect(screen.getByText('Nessuna versione fissata')).toBeInTheDocument()
})
