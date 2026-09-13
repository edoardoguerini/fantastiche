import { describe, expect, it } from 'vitest'
import {
  acceptanceFormSchema,
  inviteSchema,
  passwordRules,
  passwordRuleStatus,
  readInvitationToken,
} from '../invitation.validations'

describe('inviti', () => {
  it('richiede solo l’email e la normalizza', () => {
    expect(
      inviteSchema.parse({
        email: ' luca@example.test ',
      }),
    ).toEqual({ email: 'luca@example.test' })
    expect(inviteSchema.safeParse({ email: 'invalid' }).success).toBe(false)
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

describe('regole password', () => {
  it('espone cinque regole con etichette in italiano', () => {
    expect(passwordRules.map((rule) => rule.id)).toEqual([
      'length',
      'upper',
      'lower',
      'digit',
      'symbol',
    ])
    expect(passwordRules.map((rule) => rule.label)).toEqual([
      'Almeno 12 caratteri',
      'Una maiuscola',
      'Una minuscola',
      'Un numero',
      'Un simbolo',
    ])
  })

  it('indica quali regole soddisfa il testo digitato', () => {
    expect(passwordRuleStatus('Abc1')).toEqual({
      length: false,
      upper: true,
      lower: true,
      digit: true,
      symbol: false,
    })
    expect(passwordRuleStatus('Invited-User-123!')).toEqual({
      length: true,
      upper: true,
      lower: true,
      digit: true,
      symbol: true,
    })
  })
})

describe('schema di adesione', () => {
  it('accetta squadra e password senza campo di conferma', () => {
    const result = acceptanceFormSchema(true, true).safeParse({
      displayName: ' Giulia ',
      teamName: ' Le Fenici ',
      password: 'Invited-User-123!',
    })
    expect(result.success).toBe(true)
    expect(result.success && result.data.teamName).toBe('Le Fenici')
    expect(result.success && result.data.displayName).toBe('Giulia')
  })

  it('segnala la prima regola mancante della password', () => {
    const result = acceptanceFormSchema(false, true).safeParse({
      displayName: ' Giulia ',
      teamName: '',
      password: 'tuttominuscolo123',
    })
    expect(result.success).toBe(false)
    expect(!result.success && result.error.issues[0]?.message).toBe(
      'Aggiungi una maiuscola.',
    )
  })

  it('ignora la password quando l’account esiste già', () => {
    const result = acceptanceFormSchema(true, false).safeParse({
      displayName: ' Giulia ',
      teamName: 'Le Fenici',
      password: '',
    })
    expect(result.success).toBe(true)
  })
})

it.each(['', '   ', 'a'.repeat(151)])(
  'rifiuta un nome non valido all’attivazione: %s',
  (displayName) => {
    expect(
      acceptanceFormSchema(false, true).safeParse({
        displayName,
        teamName: '',
        password: 'Invited-User-123!',
      }).success,
    ).toBe(false)
  },
)
