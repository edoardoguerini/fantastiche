import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PwaSurfaces } from '../pwa-controls'
import * as pwa from '@/lib/pwa/pwa-store'

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

afterEach(() => {
  vi.restoreAllMocks()
  pwa.resetPwa()
})

describe('avvisi PWA', () => {
  it('non mostra nulla finché il supporto non è attivo', () => {
    state({ enabled: false, updateReady: true })
    render(<PwaSurfaces pathname="/leghe" />)
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('mostra il toast di aggiornamento fuori dall’asta e lo chiude con la X', async () => {
    state({ updateReady: true })
    const dismiss = vi.spyOn(pwa, 'dismissUpdate').mockImplementation(() => {})
    render(<PwaSurfaces pathname="/leghe" />)
    expect(screen.getByRole('status')).toHaveTextContent(
      'Nuova versione pronta',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Chiudi avviso' }))
    expect(dismiss).toHaveBeenCalled()
  })

  it('tace in sala d’asta, sugli inviti e dopo la chiusura', () => {
    state({ updateReady: true })
    const { rerender } = render(<PwaSurfaces pathname="/leghe/1/asta" />)
    expect(screen.queryByRole('status')).toBeNull()
    rerender(<PwaSurfaces pathname="/invito" />)
    expect(screen.queryByRole('status')).toBeNull()
    state({ updateReady: true, updateDismissed: true })
    rerender(<PwaSurfaces pathname="/leghe" />)
    expect(screen.queryByRole('status')).toBeNull()
  })

  it('propone l’installazione solo nella login e la rimanda con Non ora', async () => {
    state({})
    const snooze = vi.spyOn(pwa, 'snoozeInstall').mockImplementation(() => {})
    const { rerender } = render(<PwaSurfaces pathname="/leghe" />)
    expect(screen.queryByRole('region')).toBeNull()
    rerender(<PwaSurfaces pathname="/login" />)
    expect(
      screen.getByRole('region', { name: 'Installa Fantastiche' }),
    ).toHaveTextContent('Porta Fantastiche sulla Home')
    await userEvent.click(screen.getByRole('button', { name: 'Non ora' }))
    expect(snooze).toHaveBeenCalled()
  })

  it('nella login l’aggiornamento ha la precedenza e l’app installata non riceve inviti', () => {
    state({ updateReady: true })
    const { rerender } = render(<PwaSurfaces pathname="/login" />)
    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.queryByRole('region')).toBeNull()
    state({ standalone: true })
    rerender(<PwaSurfaces pathname="/login" />)
    expect(screen.queryByRole('region')).toBeNull()
    state({ installSnoozed: true })
    rerender(<PwaSurfaces pathname="/login" />)
    expect(screen.queryByRole('region')).toBeNull()
  })

  it('Installa usa il prompt nativo o mostra la guida manuale', async () => {
    state({ canPrompt: true })
    const prompt = vi.spyOn(pwa, 'promptInstall').mockResolvedValue('accepted')
    const { rerender } = render(<PwaSurfaces pathname="/login" />)
    await userEvent.click(screen.getByRole('button', { name: 'Installa' }))
    expect(prompt).toHaveBeenCalled()
    state({ canPrompt: false })
    rerender(<PwaSurfaces pathname="/login" />)
    await userEvent.click(screen.getByRole('button', { name: 'Installa' }))
    expect(screen.getByRole('region')).toHaveTextContent(
      'Aggiungi alla schermata Home',
    )
    expect(screen.getByRole('button', { name: 'Chiudi' })).toBeInTheDocument()
  })
})
