import { describe, expect, it } from 'vitest'
import {
  maxOffer,
  remainingSeconds,
  offerTotal,
  keepLatestSession,
} from '../auction-rules'

describe('regole visuali asta', () => {
  it('a parità di versione conserva il riferimento temporale più recente', () => {
    const current = { version: 2, serverTime: '2026-09-10T10:00:10Z' }
    expect(
      keepLatestSession(current, {
        version: 2,
        serverTime: '2026-09-10T10:00:05Z',
      }),
    ).toBe(current)
    const next = { version: 3, serverTime: '2026-09-10T10:00:05Z' }
    expect(keepLatestSession(current, next)).toBe(next)
  })
  it('riserva un credito per gli altri posti liberi', () => {
    expect(maxOffer(500, 25)).toBe(476)
    expect(maxOffer(10, 1)).toBe(10)
    expect(maxOffer(10, 0)).toBe(0)
    expect(maxOffer(2, 5)).toBe(0)
  })
  it('il rilancio è un totale fissato al click', () => {
    expect(offerTotal(20, 5)).toBe(25)
    expect(() => offerTotal(20, 0)).toThrow()
    expect(() => offerTotal(20, 1.5)).toThrow()
  })
  it('deriva il countdown dal tempo server e dal tempo monotono trascorso', () => {
    expect(
      remainingSeconds(
        '2026-09-10T10:00:15Z',
        '2026-09-10T10:00:00Z',
        1000,
        6500,
      ),
    ).toBe(10)
    expect(
      remainingSeconds(
        '2026-09-10T10:00:15Z',
        '2026-09-10T10:00:00Z',
        1000,
        17000,
      ),
    ).toBe(0)
  })
})
