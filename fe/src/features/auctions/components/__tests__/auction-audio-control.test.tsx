import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { AuctionAudioControl } from '../auction-audio-control'

const play = vi.fn(() => Promise.resolve())
const pause = vi.fn()
const props = {
  userId: 'manager',
  sessionId: 'session',
  canManage: true,
  active: true,
  playbackKey: 'player:1',
}

beforeEach(() => {
  localStorage.clear()
  play.mockReset().mockResolvedValue(undefined)
  pause.mockReset()
  vi.stubGlobal(
    'Audio',
    class {
      play = play
      pause = pause
      currentTime = 0
      preload = 'none'
    },
  )
})
afterEach(() => vi.unstubAllGlobals())

it('non mostra controlli e non riproduce audio ai partecipanti', () => {
  render(<AuctionAudioControl {...props} canManage={false} />)
  expect(screen.queryByRole('button')).toBeNull()
  expect(play).not.toHaveBeenCalled()
})

it('riproduce chiamate e rilanci una sola volta e conserva la scelta off', async () => {
  const view = render(<AuctionAudioControl {...props} />)
  await waitFor(() => expect(play).toHaveBeenCalledTimes(1))
  view.rerender(<AuctionAudioControl {...props} />)
  expect(play).toHaveBeenCalledTimes(1)
  view.rerender(<AuctionAudioControl {...props} playbackKey="player:2" />)
  await waitFor(() => expect(play).toHaveBeenCalledTimes(2))
  fireEvent.click(screen.getByRole('button', { name: 'Disattiva audio asta' }))
  expect(pause).toHaveBeenCalled()
  view.rerender(<AuctionAudioControl {...props} playbackKey="player:3" />)
  expect(play).toHaveBeenCalledTimes(2)
  view.unmount()
  render(<AuctionAudioControl {...props} />)
  expect(
    screen.getByRole('button', { name: 'Attiva audio asta' }),
  ).toBeVisible()
  expect(play).toHaveBeenCalledTimes(2)
})

it('consente l’attivazione dopo un blocco autoplay e arresta l’audio quando termina la chiamata', async () => {
  play.mockRejectedValueOnce(
    new DOMException('Gesture required', 'NotAllowedError'),
  )
  const view = render(<AuctionAudioControl {...props} />)
  const enable = await screen.findByRole('button', {
    name: 'Attiva audio asta',
  })
  await act(async () => fireEvent.click(enable))
  expect(play).toHaveBeenCalledTimes(2)
  expect(
    screen.getByRole('button', { name: 'Disattiva audio asta' }),
  ).toBeVisible()
  view.rerender(<AuctionAudioControl {...props} active={false} />)
  expect(pause).toHaveBeenCalled()
  view.rerender(<AuctionAudioControl {...props} canManage={false} />)
  expect(screen.queryByRole('button')).toBeNull()
})
