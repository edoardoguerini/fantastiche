import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from '@testing-library/react'
import { expect, it, vi } from 'vitest'
import { PlayerCallForm } from '../player-call-form'
import type { CatalogEntry } from '../../types/auction.types'

const player: CatalogEntry = {
  playerId: 'player-1',
  name: 'Caprile',
  role: 'P',
  clubName: 'Cagliari',
  currentQuotation: 11,
  initialQuotation: 9,
  fvm: 25,
  isAvailable: true,
  teamId: null,
}

it('chiude la selezione dalla X senza avviare una chiamata o una Bomba', () => {
  const cancel = vi.fn()
  const start = vi.fn(async () => {})
  const bomb = vi.fn(async () => {})
  render(
    <PlayerCallForm
      player={player}
      disabled={false}
      onCancel={cancel}
      onStart={start}
      onBomb={bomb}
    />,
  )
  fireEvent.click(screen.getByRole('button', { name: 'Annulla selezione' }))
  expect(cancel).toHaveBeenCalledOnce()
  expect(start).not.toHaveBeenCalled()
  expect(bomb).not.toHaveBeenCalled()
})

it('conferma timer e incrementi scelti e blocca tutti i comandi durante l’invio', async () => {
  let finish: () => void = () => {}
  const pending = new Promise<void>((resolve) => {
    finish = resolve
  })
  const start = vi.fn(() => pending)
  render(
    <PlayerCallForm
      player={player}
      disabled={false}
      onCancel={vi.fn()}
      onStart={start}
      onBomb={vi.fn(async () => {})}
    />,
  )
  fireEvent.click(screen.getByRole('button', { name: '20 secondi' }))
  const increments = within(
    screen.getByRole('group', { name: 'Incrementi dei rilanci' }),
  )
  fireEvent.click(increments.getByRole('button', { name: '+2' }))
  fireEvent.click(increments.getByRole('button', { name: '+10' }))
  fireEvent.click(screen.getByRole('button', { name: 'Chiama' }))
  await waitFor(() => expect(start).toHaveBeenCalledWith(20, [1, 2, 5]))
  for (const button of screen.getAllByRole('button'))
    expect(button).toBeDisabled()
  await act(async () => {
    finish()
    await pending
  })
  expect(
    screen.getByRole('button', { name: 'Annulla selezione' }),
  ).toBeEnabled()
})

it('distingue le quotazioni assenti dallo zero senza proporre azioni non disponibili', () => {
  render(
    <PlayerCallForm
      player={{
        ...player,
        currentQuotation: 0,
        initialQuotation: null,
        fvm: null,
      }}
      disabled
      onCancel={vi.fn()}
      onStart={vi.fn(async () => {})}
    />,
  )
  const values = within(screen.getByLabelText('Quotazioni del giocatore'))
  expect(values.getByText('Quotazione').nextElementSibling).toHaveTextContent(
    '0',
  )
  for (const name of ['Iniziale', 'Variazione', 'FVM']) {
    expect(values.getByText(name).nextElementSibling).toHaveTextContent('—')
  }
  expect(
    screen.queryByRole('button', { name: 'Sgancia la bomba' }),
  ).not.toBeInTheDocument()
  for (const button of screen.getAllByRole('button'))
    expect(button).toBeDisabled()
})
