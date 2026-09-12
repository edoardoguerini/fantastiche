import { fireEvent, render, screen } from '@testing-library/react'
import { expect, it } from 'vitest'
import { InvitationHeader } from '../invitation-header'
import type { InvitationPreview } from '../../types/invitation.types'

const preview: InvitationPreview = {
  leagueName: 'Serie Amici',
  leagueLogoUrl: 'https://cdn.example.test/logo.png',
  invitedBy: 'Marco Bianchi',
  recipientEmailHint: 'e•••o@example.test',
  expiresAt: '2026-09-14T16:30:00Z',
  requiresLogin: false,
  requiresTeam: false,
}

it('mostra il logo della lega e ripiega sulle iniziali se non si carica', () => {
  render(<InvitationHeader invitation={preview} />)
  const logo = screen.getByRole('presentation')
  expect(logo).toHaveAttribute('src', preview.leagueLogoUrl!)
  expect(screen.queryByText('SA')).toBeNull()
  fireEvent.error(logo)
  expect(screen.getByText('SA')).toBeInTheDocument()
  expect(screen.getByText('Organizzatore')).toBeInTheDocument()
})

it('nasconde la scadenza quando la data non è leggibile', () => {
  render(<InvitationHeader invitation={{ ...preview, expiresAt: 'boh' }} />)
  expect(screen.queryByText(/Scade/)).toBeNull()
})
