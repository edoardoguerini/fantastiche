import { describe, expect, it } from 'vitest'
import {
  acceptanceFormSchema,
  inviteSchema,
  readInvitationToken,
} from '../invitation.validations'

describe('inviti', () => {
  it('valida email e nome normalizzati', () => {
    expect(
      inviteSchema.parse({
        displayName: ' Luca ',
        email: ' luca@example.test ',
      }),
    ).toEqual({ displayName: 'Luca', email: 'luca@example.test' })
    expect(
      inviteSchema.safeParse({ displayName: ' ', email: 'invalid' }).success,
    ).toBe(false)
  })
  it('un nuovo partecipante richiede password conforme, conferma e nome squadra', () => {
    const schema = acceptanceFormSchema(true, true)
    const valid = {
      teamName: ' Le Fenici ',
      password: 'Invited-User-123!',
      confirmPassword: 'Invited-User-123!',
    }
    expect(schema.parse(valid).teamName).toBe('Le Fenici')
    for (const values of [
      { ...valid, teamName: '' },
      { ...valid, password: 'short', confirmPassword: 'short' },
      { ...valid, confirmPassword: 'Other-Password-123!' },
    ])
      expect(schema.safeParse(values).success).toBe(false)
  })
  it('organizzatore e account esistente non richiedono i campi estranei al loro invito', () => {
    expect(
      acceptanceFormSchema(false, false).safeParse({
        teamName: '',
        password: '',
        confirmPassword: '',
      }).success,
    ).toBe(true)
    expect(
      acceptanceFormSchema(true, false).safeParse({
        teamName: 'Le Fenici',
        password: '',
        confirmPassword: '',
      }).success,
    ).toBe(true)
  })
  it('legge il frammento e la query legacy senza accettare token arbitrari', () => {
    const token = 'a'.repeat(64)
    expect(
      readInvitationToken(`https://fantastiche.test/invito#token=${token}`),
    ).toBe(token)
    expect(
      readInvitationToken(`https://fantastiche.test/invito?token=${token}`),
    ).toBe(token)
    expect(
      readInvitationToken('https://fantastiche.test/invito?token=bad'),
    ).toBeNull()
    expect(readInvitationToken('https://fantastiche.test/invito')).toBeNull()
  })
})
