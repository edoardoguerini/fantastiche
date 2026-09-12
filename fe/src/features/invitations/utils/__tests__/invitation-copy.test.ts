import { describe, expect, it } from 'vitest'
import { ApiError } from '@/lib/api/error'
import { formatExpiry, invitationErrorCopy } from '../invitation-copy'

describe('scadenza', () => {
  it('scrive giorno, mese e ora in italiano', () => {
    expect(formatExpiry('2026-09-14T16:30:00Z', 'Europe/Rome')).toBe(
      'lunedì 14 settembre alle 18:30',
    )
  })

  it('non esplode con una data non valida', () => {
    expect(formatExpiry('non-una-data')).toBe('')
  })
})

describe('testi di errore', () => {
  it('distingue scaduto, revocato e già accettato dal codice', () => {
    expect(
      invitationErrorCopy(new ApiError(410, 'invitation.expired', 'Scaduto.')),
    ).toMatchObject({ title: 'Questo invito è scaduto.', retryable: false })
    expect(
      invitationErrorCopy(new ApiError(410, 'invitation.revoked', 'Revocato.')),
    ).toMatchObject({ title: 'Questo invito è stato revocato.' })
    expect(
      invitationErrorCopy(
        new ApiError(410, 'invitation.consumed', 'Già accettato.'),
      ),
    ).toMatchObject({ title: 'Hai già accettato questo invito.' })
  })

  it('tratta un link sconosciuto come incompleto', () => {
    expect(
      invitationErrorCopy(new ApiError(404, 'not_found', 'Non trovato.')),
    ).toMatchObject({ title: 'Invito non trovato.', retryable: false })
  })

  it('invita a riprovare per errori di rete o del servizio', () => {
    expect(
      invitationErrorCopy(new ApiError(0, 'network', 'Rete.')),
    ).toMatchObject({
      title: 'Non riusciamo a verificare l’invito.',
      retryable: true,
    })
    expect(invitationErrorCopy(new Error('boom'))).toMatchObject({
      retryable: true,
    })
  })
})
