import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type * as ReactRouter from '@tanstack/react-router'
import { afterEach, expect, it, vi } from 'vitest'
import { authQueryOptions, type AuthenticatedUser } from '@/features/auth'
import { api } from '@/lib/api/client'
import { CatalogImportForm } from '../catalog-import-form'
import { LeagueCatalogPanel } from '../league-catalog-panel'
import { CatalogPublishControl } from '../catalog-publish-control'
import { CatalogEntriesPreview } from '../catalog-entries-preview'
import { CatalogPage } from '../catalog-page'

vi.mock('@tanstack/react-router', async (importOriginal) => ({
  ...(await importOriginal<typeof ReactRouter>()),
  Link: ({ children }: { children: React.ReactNode }) => (
    <span>{children}</span>
  ),
}))
vi.mock('@/lib/api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))
const uid = '00000000-0000-4000-8000-000000000001'
const vid = '00000000-0000-4000-8000-000000000002'
const version = {
  id: vid,
  seasonName: '2026/27',
  status: 'Published' as const,
  source: 'FantacalcioCsv',
  contentHash: 'abc',
  entryCount: 20,
  createdAt: '2026-09-10T10:00:00Z',
  publishedAt: '2026-09-10T11:00:00Z',
}
function wrapper(isSuperAdmin = true) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const authenticated: AuthenticatedUser = {
    id: uid,
    isSuperAdmin,
    email: 'demo@example.test',
    displayName: 'Demo',
  }
  client.setQueryData(authQueryOptions().queryKey, () => authenticated)
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}
afterEach(() => vi.resetAllMocks())

it('importa il CSV originale con stagione esplicita, senza pubblicarlo automaticamente', async () => {
  const user = userEvent.setup()
  const csv =
    '1,Rossi,Mario Rossi,P,Por,1,1,1,1,Club,1,1,destro,Italia,01/01/2000,url,0,0,0\n'
  const file = new File([csv], 'listone.csv', { type: 'text/csv' })
  Object.defineProperty(file, 'text', { value: async () => csv })
  vi.mocked(api.post).mockResolvedValue({
    ...version,
    status: 'Draft',
    publishedAt: null,
  })
  const onImported = vi.fn()
  render(<CatalogImportForm userId={uid} onImported={onImported} />, {
    wrapper: wrapper(),
  })
  await user.type(screen.getByLabelText('Stagione del listone'), '2026/27')
  await user.upload(screen.getByLabelText('File CSV Fantacalcio'), file)
  await user.click(screen.getByRole('button', { name: 'Importa in bozza' }))
  await waitFor(() => expect(onImported).toHaveBeenCalled())
  expect(api.post).toHaveBeenCalledTimes(1)
  expect(api.post).toHaveBeenCalledWith(
    '/Catalog/Imports',
    { seasonName: '2026/27', csv },
    expect.any(AbortSignal),
  )
})

it('non invia due importazioni mentre la prima è in corso', async () => {
  const user = userEvent.setup()
  const file = new File(['csv'], 'listone.csv', { type: 'text/csv' })
  Object.defineProperty(file, 'text', { value: async () => 'csv' })
  let resolve!: (value: unknown) => void
  vi.mocked(api.post).mockImplementation(
    () =>
      new Promise((done) => {
        resolve = done
      }),
  )
  render(<CatalogImportForm userId={uid} onImported={vi.fn()} />, {
    wrapper: wrapper(),
  })
  await user.type(screen.getByLabelText('Stagione del listone'), '2026/27')
  await user.upload(screen.getByLabelText('File CSV Fantacalcio'), file)
  const form = screen
    .getByRole('button', { name: 'Importa in bozza' })
    .closest('form')!
  fireEvent.submit(form)
  await waitFor(() => expect(api.post).toHaveBeenCalledTimes(1))
  fireEvent.submit(form)
  expect(api.post).toHaveBeenCalledTimes(1)
  await act(async () =>
    resolve({ ...version, status: 'Draft', publishedAt: null }),
  )
})

it('mostra il listone già scelto senza offrire sostituzioni', async () => {
  vi.mocked(api.get).mockImplementation(async (path) =>
    path.endsWith('/AuctionRoom')
      ? { canManage: true }
      : path.endsWith('/Catalog')
        ? version
        : { items: [], page: 1, pageSize: 30, total: 0 },
  )
  render(
    <LeagueCatalogPanel
      leagueId="league"
      seasonId="season"
      seasonName="2026/27"
    />,
    { wrapper: wrapper() },
  )
  await screen.findByText(/non può essere sostituito/i)
  expect(
    screen.queryByRole('button', { name: /scegli questo listone/i }),
  ).not.toBeInTheDocument()
  expect(api.put).not.toHaveBeenCalled()
})

it('propone solo versioni pubblicate della stessa stagione e seleziona su conferma', async () => {
  const user = userEvent.setup()
  let selected = false
  vi.mocked(api.get).mockImplementation(async (path) => {
    if (path.endsWith('/AuctionRoom')) return { canManage: true }
    if (path.endsWith('/Catalog')) return selected ? version : null
    return {
      items: [
        version,
        { ...version, id: 'draft', status: 'Draft' },
        { ...version, id: 'old', seasonName: '2025/26' },
      ],
      page: 1,
      pageSize: 10,
      total: 3,
    }
  })
  vi.mocked(api.put).mockImplementation(async () => {
    selected = true
    return { leagueId: 'league', leagueSeasonId: 'season', listVersionId: vid }
  })
  render(
    <LeagueCatalogPanel
      leagueId="league"
      seasonId="season"
      seasonName="2026/27"
    />,
    { wrapper: wrapper(false) },
  )
  const choose = await screen.findAllByRole('button', {
    name: /scegli questo listone/i,
  })
  expect(choose).toHaveLength(1)
  await user.click(choose[0]!)
  expect(api.put).not.toHaveBeenCalled()
  await user.click(
    screen.getByRole('button', { name: 'Conferma scelta del listone' }),
  )
  await waitFor(() =>
    expect(api.put).toHaveBeenCalledWith(
      '/Leagues/league/Seasons/season/Catalog',
      { listVersionId: vid },
      expect.any(AbortSignal),
    ),
  )
})

it('pubblica la bozza soltanto con un gesto esplicito', async () => {
  const user = userEvent.setup()
  vi.mocked(api.post).mockResolvedValue(version)
  const onPublished = vi.fn()
  render(
    <CatalogPublishControl
      userId={uid}
      version={{ ...version, status: 'Draft', publishedAt: null }}
      onPublished={onPublished}
    />,
    { wrapper: wrapper() },
  )
  expect(api.post).not.toHaveBeenCalled()
  await user.click(screen.getByRole('button', { name: 'Pubblica listone' }))
  await waitFor(() => expect(onPublished).toHaveBeenCalledWith(version))
  expect(api.post).toHaveBeenCalledWith(
    `/Catalog/Versions/${vid}/Publish`,
    {},
    expect.any(AbortSignal),
  )
})

it('non carica la gestione del catalogo per un utente non SuperAdmin', async () => {
  render(<CatalogPage />, { wrapper: wrapper(false) })
  await screen.findByText('Accesso riservato')
  expect(
    vi.mocked(api.get).mock.calls.some(([path]) => path.startsWith('/Catalog')),
  ).toBe(false)
  expect(
    screen.queryByLabelText('File CSV Fantacalcio'),
  ).not.toBeInTheDocument()
})

it('non propone una scelta al membro senza permesso di gestione', async () => {
  vi.mocked(api.get).mockImplementation(async (path) =>
    path.endsWith('/AuctionRoom') ? { canManage: false } : null,
  )
  render(
    <LeagueCatalogPanel
      leagueId="league"
      seasonId="season"
      seasonName="2026/27"
    />,
    { wrapper: wrapper(false) },
  )
  await screen.findByText(/organizzatore deve ancora scegliere/i)
  expect(
    screen.queryByRole('button', { name: /scegli questo listone/i }),
  ).not.toBeInTheDocument()
  expect(api.get).not.toHaveBeenCalledWith(
    expect.stringContaining('/Catalog/Versions?'),
    expect.anything(),
  )
})

it('trasmette ricerca, ruolo e club insieme e riparte dalla prima pagina', async () => {
  const user = userEvent.setup()
  vi.mocked(api.get).mockResolvedValue({
    items: [],
    page: 1,
    pageSize: 30,
    total: 61,
  })
  render(<CatalogEntriesPreview userId={uid} version={version} />, {
    wrapper: wrapper(),
  })
  await user.click(await screen.findByRole('button', { name: 'Successiva' }))
  await waitFor(() =>
    expect(api.get).toHaveBeenLastCalledWith(
      expect.stringContaining('page=2'),
      expect.any(AbortSignal),
    ),
  )
  await user.type(screen.getByLabelText('Cerca calciatore'), 'Rossi')
  await user.selectOptions(screen.getByLabelText('Ruolo'), 'P')
  await user.type(screen.getByLabelText('Club'), 'Roma')
  await user.click(screen.getByRole('button', { name: 'Filtra' }))
  await waitFor(() =>
    expect(api.get).toHaveBeenLastCalledWith(
      `/Catalog/Versions/${vid}/Entries?search=Rossi&role=P&club=Roma&page=1&pageSize=30`,
      expect.any(AbortSignal),
    ),
  )
})
