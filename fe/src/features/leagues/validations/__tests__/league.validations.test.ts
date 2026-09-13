import { describe, expect, it } from 'vitest'
import { createLeagueSchema } from '../league.validations'

const valid = {
  name: 'Lega amici',
  seasonName: '2026/27',
  organizerEmail: 'giulia@example.test',
  budget: 500,
  goalkeepers: 3,
  defenders: 8,
  midfielders: 8,
  forwards: 6,
}

describe('creazione lega', () => {
  it('normalizza i testi prima di inviarli', () => {
    expect(
      createLeagueSchema.parse({
        ...valid,
        name: ' Lega amici ',
        organizerEmail: ' giulia@example.test ',
      }),
    ).toEqual(valid)
  })
  it.each([
    { name: '   ' },
    { organizerEmail: 'non-email' },
    { seasonName: '' },
    { budget: 24 },
    { budget: 1000001 },
    { goalkeepers: -1 },
    { forwards: 1.5 },
    { defenders: 100 },
    { goalkeepers: 0, defenders: 0, midfielders: 0, forwards: 0 },
  ])('rifiuta valori non validi: %o', (changes) => {
    expect(createLeagueSchema.safeParse({ ...valid, ...changes }).success).toBe(
      false,
    )
  })
  it('consente ruoli vuoti e un credito per ogni posto in rosa', () => {
    expect(
      createLeagueSchema.safeParse({
        ...valid,
        budget: 1,
        goalkeepers: 0,
        defenders: 0,
        midfielders: 0,
        forwards: 1,
      }).success,
    ).toBe(true)
  })
})
