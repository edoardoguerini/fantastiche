import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type * as ReactRouter from '@tanstack/react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/lib/api/client'
import { ApiError } from '@/lib/api/error'
import { InvitationPage } from '../invitation-page'
import type { InvitationPreview } from '../../types/invitation.types'

const navigate = vi.fn()
vi.mock('@tanstack/react-router', async (importOriginal) => ({
  ...(await importOriginal<typeof ReactRouter>()),
  Link: ({ children, to }: { children: React.ReactNode; to: string }) => (
    <a href={to}>{children}</a>
  ),
  useNavigate: () => navigate,
}))
vi.mock('@/lib/api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))
// In jsdom la sessione va letta con il ramo client, come nel browser.
vi.mock('@tanstack/react-start', () => ({
  createIsomorphicFn: () => {
    const builder = {
      server: () => builder,
      client: <T,>(fn: T) => fn,
    }
    return builder
  },
}))
afterEach(() => {
  vi.resetAllMocks()
  window.history.replaceState(null, '', '/')
})

const token = 'a'.repeat(64)
const account = {
  id: 'user-1',
  email: 'edoardo@example.test',
  displayName: 'Edoardo',
  isSuperAdmin: false,
}
const preview: InvitationPreview = {
  leagueName: 'Serie Amici',
  leagueLogoUrl: null,
  invitedBy: 'Marco Bianchi',
  recipientEmailHint: 'e•••o@example.test',
  expiresAt: '2026-09-14T16:30:00Z',
  requiresLogin: false,
  requiresTeam: true,
}
function setup({
  session = null,
  invitation = {},
  previewError,
}: {
  session?: typeof account | null
  invitation?: Partial<InvitationPreview>
  previewError?: unknown
} = {}) {
  vi.mocked(api.get).mockImplementation(async (path: string) => {
    if (path === '/Auth/Me') {
      if (session) return session
      throw new ApiError(401, 'auth.required', 'Accesso richiesto.')
    }
    if (path === '/Invitations/Preview') {
      if (previewError) throw previewError
      return { ...preview, ...invitation }
    }
    throw new Error(`Richiesta inattesa: ${path}`)
  })
  window.history.replaceState(null, '', `/invito#token=${token}`)
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  render(
    <QueryClientProvider client={client}>
      <InvitationPage />
    </QueryClientProvider>,
  )
  return { user: userEvent.setup() }
}

describe('pagina invito', () => {
  it('presenta la lega con iniziali, ruolo, mittente e scadenza', async () => {
    setup()
    expect(
      await screen.findByRole('heading', { name: 'Serie Amici' }),
    ).toBeVisible()
    expect(screen.getByText('SA')).toBeInTheDocument()
    expect(screen.getByText('Partecipante')).toBeInTheDocument()
    expect(screen.getByText(/Marco Bianchi/)).toBeInTheDocument()
    expect(screen.getByText(/Scade .*settembre/)).toBeInTheDocument()
    expect(window.location.hash).toBe('')
  })

  it('spiega un invito scaduto e rimanda al login', async () => {
    setup({
      previewError: new ApiError(410, 'invitation.expired', 'Invito scaduto.'),
    })
    expect(
      await screen.findByRole('heading', { name: 'Questo invito è scaduto.' }),
    ).toBeVisible()
    expect(screen.getByRole('alert')).toHaveTextContent(/scaduto/)
    expect(screen.getByRole('link', { name: /Accedi/ })).toHaveAttribute(
      'href',
      '/login',
    )
    expect(screen.queryByLabelText('Password', { exact: true })).toBeNull()
  })

  it('guida l’account esistente con il passo di accesso e l’email mascherata', async () => {
    setup({ invitation: { requiresLogin: true } })
    expect(await screen.findByText('e•••o@example.test')).toBeInTheDocument()
    expect(screen.getByText('Accedi', { selector: 'li *' })).toBeInTheDocument()
    expect(screen.getByRole('list', { name: 'Passaggi' })).toBeInTheDocument()
    expect(screen.getByRole('listitem', { current: 'step' })).toHaveTextContent(
      'Accedi',
    )
    expect(screen.getByLabelText('Email')).toBeVisible()
  })

  it('mostra l’identità con cui si accetta e passa alla conferma', async () => {
    setup({ session: account, invitation: { requiresLogin: true } })
    expect(await screen.findByText(/Stai accettando come/)).toHaveTextContent(
      account.email,
    )
    expect(screen.getByRole('listitem', { current: 'step' })).toHaveTextContent(
      'Conferma squadra',
    )
    expect(screen.getByLabelText('Nome squadra', { exact: true })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Cambia account' })).toBeVisible()
  })

  it('chiede di uscire quando un account collegato apre un invito per un account nuovo', async () => {
    setup({ session: account })
    expect(
      await screen.findByText(/attiva un nuovo account/),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Password', { exact: true })).toBeNull()
    expect(screen.getByRole('button', { name: 'Cambia account' })).toBeVisible()
  })

  it('mostra un errore di logout accanto a Cambia account senza nascondere l’invito', async () => {
    vi.mocked(api.post).mockRejectedValue(
      new ApiError(0, 'network', 'Rete assente.'),
    )
    const { user } = setup({
      session: account,
      invitation: { requiresLogin: true },
    })
    await user.click(
      await screen.findByRole('button', { name: 'Cambia account' }),
    )
    expect(await screen.findByRole('alert')).toHaveTextContent(/Rete assente/)
    expect(screen.getByRole('heading', { name: 'Serie Amici' })).toBeVisible()
    expect(screen.getByLabelText('Nome squadra', { exact: true })).toBeVisible()
  })

  it('account esistente: dopo la conferma entra in lega senza mostrare un login', async () => {
    vi.mocked(api.post).mockResolvedValue({
      leagueId: 'league',
      leagueSeasonId: 'season',
      teamId: 'team',
      email: account.email,
      teamName: 'Le Fenici',
    })
    const { user } = setup({
      session: account,
      invitation: { requiresLogin: true },
    })
    await user.type(
      await screen.findByLabelText('Nome squadra', { exact: true }),
      'Le Fenici',
    )
    await user.click(
      screen.getByRole('button', { name: 'Entra in Serie Amici' }),
    )
    await waitFor(() =>
      expect(navigate).toHaveBeenCalledWith(
        expect.objectContaining({ params: { leagueId: 'league' } }),
      ),
    )
    expect(screen.queryByText(/password appena scelta/)).toBeNull()
    expect(screen.queryByRole('button', { name: 'Accedi' })).toBeNull()
    expect(screen.getByRole('status')).toHaveTextContent(/Entriamo in lega/)
  })

  it('espone sempre un titolo per la sezione durante la verifica', () => {
    vi.mocked(api.get).mockImplementation(() => new Promise(() => {}))
    window.history.replaceState(null, '', `/invito#token=${token}`)
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    })
    render(
      <QueryClientProvider client={client}>
        <InvitationPage />
      </QueryClientProvider>,
    )
    expect(screen.getByRole('heading', { level: 1 })).toHaveAttribute(
      'id',
      'invitation-title',
    )
  })

  it('dopo l’attivazione precompila l’email nel login', async () => {
    vi.mocked(api.post).mockImplementation(async (path: string) => {
      if (path === '/Invitations/Accept')
        return {
          leagueId: 'league',
          leagueSeasonId: 'season',
          teamId: 'team',
          email: account.email,
          teamName: 'Le Fenici',
        }
      throw new Error(`Richiesta inattesa: ${path}`)
    })
    const { user } = setup()
    await user.type(await screen.findByLabelText('Il tuo nome'), 'Edoardo')
    await user.type(
      await screen.findByLabelText('Nome squadra', { exact: true }),
      'Le Fenici',
    )
    await user.type(
      screen.getByLabelText('Password', { exact: true }),
      'Invited-User-123!',
    )
    await user.click(
      screen.getByRole('button', { name: 'Attiva account e partecipa' }),
    )
    expect(
      await screen.findByRole('heading', { name: 'Sei in Serie Amici.' }),
    ).toBeVisible()
    expect(screen.getByText(/Le Fenici/)).toBeInTheDocument()
    const email = screen.getByLabelText('Email')
    expect(email).toHaveValue(account.email)
    expect(email).toHaveAttribute('readonly')
    await waitFor(() =>
      expect(screen.getByLabelText('Password', { exact: true })).toHaveFocus(),
    )
  })
})
