import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '@/lib/api/client'
import { ApiError } from '@/lib/api/error'
import { AcceptInvitationForm } from '../accept-invitation-form'
import type { InvitationPreview } from '../../types/invitation.types'

vi.mock('@/lib/api/client', () => ({
  api: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))
afterEach(() => vi.resetAllMocks())

const token = 'a'.repeat(64)
const preview: InvitationPreview = {
  leagueName: 'Serie Amici',
  leagueLogoUrl: null,
  invitedBy: 'Marco Bianchi',
  recipientEmailHint: 'e•••o@example.test',
  expiresAt: '2026-09-14T16:30:00Z',
  requiresLogin: false,
  requiresTeam: true,
}
const accepted = {
  leagueId: 'league',
  leagueSeasonId: 'season',
  teamId: 'team',
  email: 'edoardo@example.test',
  teamName: 'Le Fenici',
}
function setup(invitation: Partial<InvitationPreview> = {}) {
  const onAccepted = vi.fn()
  const onUnavailable = vi.fn()
  render(
    <AcceptInvitationForm
      token={token}
      invitation={{ ...preview, ...invitation }}
      onAccepted={onAccepted}
      onUnavailable={onUnavailable}
    />,
  )
  return { user: userEvent.setup(), onAccepted, onUnavailable }
}
const ruleItem = (label: string) =>
  screen.getByText(label, { exact: true }).closest('li')

describe('adesione con account nuovo', () => {
  it('aggiorna la checklist della password mentre si scrive e permette di mostrarla', async () => {
    const { user } = setup()
    expect(ruleItem('Una maiuscola')).toHaveAttribute('data-satisfied', 'false')
    expect(ruleItem('Una maiuscola')).toHaveTextContent(/mancante/)
    await user.type(screen.getByLabelText('Password', { exact: true }), 'Abc1')
    expect(ruleItem('Una maiuscola')).toHaveAttribute('data-satisfied', 'true')
    expect(ruleItem('Una maiuscola')).toHaveTextContent(/soddisfatto/)
    expect(ruleItem('Un numero')).toHaveAttribute('data-satisfied', 'true')
    expect(ruleItem('Almeno 12 caratteri')).toHaveAttribute(
      'data-satisfied',
      'false',
    )
    await user.click(screen.getByRole('button', { name: 'Mostra password' }))
    expect(screen.getByLabelText('Password', { exact: true })).toHaveAttribute(
      'type',
      'text',
    )
    expect(screen.queryByLabelText('Conferma password')).toBeNull()
  })

  it('mostra l’anteprima del nome squadra e invia squadra e password', async () => {
    vi.mocked(api.post).mockResolvedValue(accepted)
    const { user, onAccepted } = setup()
    const team = screen.getByLabelText('Nome squadra', { exact: true })
    expect(team).toHaveFocus()
    await user.type(team, 'Le Fenici')
    expect(screen.getByText(/apparirai come/)).toHaveTextContent('Le Fenici')
    await user.type(
      screen.getByLabelText('Password', { exact: true }),
      'Invited-User-123!',
    )
    await user.click(
      screen.getByRole('button', { name: 'Attiva account e partecipa' }),
    )
    await waitFor(() => expect(onAccepted).toHaveBeenCalledWith(accepted))
    expect(api.post).toHaveBeenCalledWith('/Invitations/Accept', {
      token,
      teamName: 'Le Fenici',
      password: 'Invited-User-123!',
    })
  })

  it('blocca una password debole senza chiamare il server', async () => {
    const { user } = setup({ requiresTeam: false })
    await user.type(
      screen.getByLabelText('Password', { exact: true }),
      'tuttominuscolo123',
    )
    await user.click(
      screen.getByRole('button', { name: 'Attiva account e organizza' }),
    )
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Aggiungi una maiuscola.',
    )
    expect(api.post).not.toHaveBeenCalled()
  })
})

describe('adesione con account esistente', () => {
  it('chiede solo il nome squadra e non invia la password', async () => {
    vi.mocked(api.post).mockResolvedValue(accepted)
    const { user, onAccepted } = setup({ requiresLogin: true })
    expect(screen.queryByLabelText('Password', { exact: true })).toBeNull()
    await user.type(
      screen.getByLabelText('Nome squadra', { exact: true }),
      'Le Fenici',
    )
    await user.click(
      screen.getByRole('button', { name: 'Entra in Serie Amici' }),
    )
    await waitFor(() => expect(onAccepted).toHaveBeenCalledOnce())
    expect(api.post).toHaveBeenCalledWith('/Invitations/Accept', {
      token,
      teamName: 'Le Fenici',
    })
  })

  it('spiega l’account sbagliato e offre il cambio account', async () => {
    vi.mocked(api.post).mockRejectedValue(
      new ApiError(403, 'auth.forbidden', 'Operazione non consentita.'),
    )
    const onSwitchAccount = vi.fn()
    render(
      <AcceptInvitationForm
        token={token}
        invitation={{ ...preview, requiresLogin: true }}
        onAccepted={vi.fn()}
        onUnavailable={vi.fn()}
        onSwitchAccount={onSwitchAccount}
      />,
    )
    const user = userEvent.setup()
    await user.type(
      screen.getByLabelText('Nome squadra', { exact: true }),
      'Le Fenici',
    )
    await user.click(
      screen.getByRole('button', { name: 'Entra in Serie Amici' }),
    )
    expect(await screen.findByRole('alert')).toHaveTextContent(/destinatario/)
    await user.click(screen.getByRole('button', { name: 'Cambia account' }))
    expect(onSwitchAccount).toHaveBeenCalledOnce()
  })
})
