import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PwaSurfaces } from '../pwa-controls'
import * as pwa from '@/lib/pwa/pwa-store'

function state(patch: Partial<pwa.PwaState>) {
  vi.spyOn(pwa, 'getSnapshot').mockReturnValue({
    enabled: true,
    standalone: false,
    installed: false,
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
    state({ updateReady: true, standalone: true })
    const dismiss = vi.spyOn(pwa, 'dismissUpdate').mockImplementation(() => {})
    render(<PwaSurfaces pathname="/leghe" />)
    expect(screen.getByRole('status')).toHaveTextContent(
      'Nuova versione pronta',
    )
    await userEvent.click(screen.getByRole('button', { name: 'Chiudi avviso' }))
    expect(dismiss).toHaveBeenCalled()
  })

  it('tace in sala d’asta, sugli inviti e dopo la chiusura', () => {
    state({ updateReady: true, standalone: true })
    const { rerender } = render(<PwaSurfaces pathname="/leghe/1/asta" />)
    expect(screen.queryByRole('status')).toBeNull()
    rerender(<PwaSurfaces pathname="/invito" />)
    expect(screen.queryByRole('status')).toBeNull()
    state({ updateReady: true, updateDismissed: true, standalone: true })
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

  it('nella login della PWA mostra l’aggiornamento senza inviti a installare', () => {
    state({ updateReady: true, standalone: true })
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

  it.each([false, true])(
    'nel browser l’aggiornamento non nasconde l’installazione (avviso chiuso: %s)',
    (updateDismissed) => {
      state({ updateReady: true, updateDismissed })
      const { rerender } = render(<PwaSurfaces pathname="/login" />)
      expect(screen.queryByRole('status')).toBeNull()
      expect(
        screen.getByRole('region', { name: 'Installa Fantastiche' }),
      ).toBeInTheDocument()
      rerender(<PwaSurfaces pathname="/leghe" />)
      expect(screen.queryByRole('status')).toBeNull()
    },
  )

  it('dopo l’installazione la scheda browser non mostra né aggiornamento né invito', () => {
    state({ installed: true, updateReady: true })
    render(<PwaSurfaces pathname="/login" />)
    expect(screen.queryByRole('status')).toBeNull()
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
