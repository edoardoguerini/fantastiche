import { act, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { RosterExport } from '../roster-export'

afterEach(() => vi.unstubAllGlobals())

it('annulla il trasporto quando la sala viene smontata e ignora la risposta tardiva', async () => {
  let signal: AbortSignal | null | undefined
  let respond!: (response: Response) => void
  vi.stubGlobal('fetch', (_input: unknown, init: RequestInit) => {
    signal = init.signal
    return new Promise<Response>((resolve) => {
      respond = resolve
    })
  })
  const createObjectURL = vi.fn()
  const OriginalURL = URL
  class TestURL extends OriginalURL {
    static createObjectURL = createObjectURL
  }
  vi.stubGlobal('URL', TestURL)
  const { unmount } = render(
    <RosterExport sessionId="session-1" completed={false} />,
  )
  fireEvent.click(
    screen.getByRole('button', { name: 'Esporta per Fantacalcio.it' }),
  )
  expect(signal?.aborted).toBe(false)
  unmount()
  expect(signal?.aborted).toBe(true)
  await act(async () => {
    respond(
      new Response(
        JSON.stringify({
          isSuccess: true,
          errors: [],
          data: {
            fileName: 'fantastiche-rosters-test.csv',
            csv: '$,$,$\nPrima,123,1\n',
          },
        }),
        { headers: { 'Content-Type': 'application/json' } },
      ),
    )
  })
  expect(createObjectURL).not.toHaveBeenCalled()
})
