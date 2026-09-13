import { afterEach, expect, it, vi } from 'vitest'
import userEvent from '@testing-library/user-event'
import { render, screen, within } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { api } from '@/lib/api/client'
import { ParticipantsPanel } from '../participants-panel'
import { ParticipantsSummary } from '../participants-summary'

vi.mock('@/lib/api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))

const uid = '00000000-0000-4000-8000-000000000001'
const payload = {
  leagueId: 'league',
  leagueSeasonId: 'season',
  canManage: true,
  participants: [
    {
      userId: 'u1',
      displayName: 'Luca Ferri',
      teamName: 'Atletico Spritz',
      isOrganizer: true,
    },
    {
      userId: 'u2',
      displayName: 'Andrea Galli',
      teamName: 'Borussia Porcelli',
      isOrganizer: false,
    },
  ],
  invitations: {
    items: [
      {
        id: 'i1',
        displayName: 'Sara Moretti',
        email: 'sara@example.test',
        kind: 'Participant',
        status: 'Pending',
        expiresAt: '2026-09-20T10:00:00Z',
      },
      {
        id: 'i2',
        displayName: 'Andrea Galli',
        email: 'andrea@example.test',
        kind: 'Participant',
        status: 'Accepted',
        expiresAt: '2026-09-20T10:00:00Z',
      },
    ],
    totalCount: 2,
    page: 1,
    pageSize: 20,
  },
}
function wrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
}
afterEach(() => vi.resetAllMocks())

it('mostra le squadre in evidenza, il badge organizzatore e il form di invito', async () => {
  vi.mocked(api.get).mockResolvedValue(payload)
  render(
    <ParticipantsPanel userId={uid} leagueId="league" seasonId="season" />,
    { wrapper: wrapper() },
  )
  const heading = await screen.findByRole('heading', { name: /Partecipanti/ })
  expect(heading).toHaveTextContent('2')
  const rows = screen.getAllByRole('listitem')
  expect(within(rows[0]!).getByText('Atletico Spritz')).toBeInTheDocument()
  expect(within(rows[0]!).getByText('Luca Ferri')).toBeInTheDocument()
  expect(within(rows[0]!).getByText('Organizzatore')).toBeInTheDocument()
  expect(within(rows[1]!).queryByText(/Organizzatore/)).not.toBeInTheDocument()
  expect(
    screen.getByRole('heading', { name: 'Invita un partecipante' }),
  ).toBeInTheDocument()
  expect(screen.queryByLabelText('Nome partecipante')).not.toBeInTheDocument()
  expect(screen.getByLabelText('Email partecipante')).toBeInTheDocument()
  expect(screen.getByRole('heading', { name: /^Inviti/ })).toHaveTextContent(
    '2',
  )
  expect(screen.queryByText('LA TUA LEGA')).not.toBeInTheDocument()
})

it('non mostra nulla a chi non può gestire la lega', async () => {
  vi.mocked(api.get).mockResolvedValue({
    ...payload,
    canManage: false,
    participants: [],
    invitations: { items: [], totalCount: 0, page: 1, pageSize: 20 },
  })
  const { container } = render(
    <>
      <ParticipantsPanel userId={uid} leagueId="league" seasonId="season" />
      <ParticipantsSummary userId={uid} leagueId="league" seasonId="season" />
    </>,
    { wrapper: wrapper() },
  )
  await vi.waitFor(() => expect(api.get).toHaveBeenCalled())
  await vi.waitFor(() => expect(container).toBeEmptyDOMElement())
})

it('riassume squadre e inviti in attesa', async () => {
  vi.mocked(api.get).mockResolvedValue(payload)
  render(
    <dl>
      <ParticipantsSummary userId={uid} leagueId="league" seasonId="season" />
    </dl>,
    { wrapper: wrapper() },
  )
  expect(await screen.findByRole('term')).toHaveTextContent('Squadre')
  expect(screen.getByRole('definition')).toHaveTextContent('2')
  expect(screen.getByText('1 invito in attesa')).toBeInTheDocument()
})

it('raccoglie gli inviti conclusi nello storico e mantiene le azioni sugli inviti aperti', async () => {
  const user = userEvent.setup()
  vi.mocked(api.get).mockResolvedValue(payload)
  render(
    <ParticipantsPanel userId={uid} leagueId="league" seasonId="season" />,
    { wrapper: wrapper() },
  )
  await screen.findByRole('heading', { name: /^Inviti/ })
  expect(screen.getByText('Accettato')).not.toBeVisible()
  expect(screen.getAllByRole('button', { name: /Azioni invito/ })).toHaveLength(
    1,
  )
  await user.click(screen.getByText('Storico inviti'))
  expect(screen.getByText('Accettato')).toBeVisible()
  expect(
    screen.queryByRole('button', { name: /Azioni invito Andrea/ }),
  ).not.toBeInTheDocument()
})

it('apre le azioni da tastiera, chiude con Escape e chiede conferma prima della revoca', async () => {
  const user = userEvent.setup()
  vi.mocked(api.get).mockResolvedValue(payload)
  vi.mocked(api.post).mockResolvedValue({})
  render(
    <ParticipantsPanel userId={uid} leagueId="league" seasonId="season" />,
    { wrapper: wrapper() },
  )
  const trigger = await screen.findByRole('button', {
    name: /Azioni invito Sara Moretti/,
  })
  expect(
    screen.queryByRole('button', { name: 'Revoca invito' }),
  ).not.toBeInTheDocument()
  trigger.focus()
  await user.keyboard('{Enter}')
  await user.tab()
  expect(screen.getByRole('button', { name: 'Reinvia invito' })).toHaveFocus()
  await user.keyboard('{Escape}')
  expect(trigger).toHaveFocus()
  expect(trigger).toHaveAttribute('aria-expanded', 'false')
  await user.click(trigger)
  await user.click(screen.getByRole('heading', { name: /^Partecipanti/ }))
  expect(trigger).toHaveAttribute('aria-expanded', 'false')
  await user.click(trigger)
  await user.click(screen.getByRole('button', { name: 'Revoca invito' }))
  expect(api.post).not.toHaveBeenCalled()
  await user.click(screen.getByRole('button', { name: 'Conferma revoca' }))
  expect(await screen.findByText('Invito revocato.')).toBeVisible()
})

it('usa l’email una sola volta e nelle azioni per un invito senza nome', async () => {
  vi.mocked(api.get).mockResolvedValue({
    ...payload,
    invitations: {
      ...payload.invitations,
      items: [{ ...payload.invitations.items[0], displayName: '' }],
    },
  })
  render(
    <ParticipantsPanel userId={uid} leagueId="league" seasonId="season" />,
    { wrapper: wrapper() },
  )
  const trigger = await screen.findByRole('button', {
    name: 'Azioni invito sara@example.test',
  })
  const row = trigger.closest('li')!
  expect(within(row).getAllByText('sara@example.test')).toHaveLength(1)
  expect(row.querySelector('.participant-avatar')).toHaveTextContent('S')
})
