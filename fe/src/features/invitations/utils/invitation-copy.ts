import { ApiError } from '@/lib/api/error'

// Data assoluta leggibile: "lunedì 14 settembre alle 18:30".
export function formatExpiry(iso: string, timeZone?: string) {
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return ''
  const day = new Intl.DateTimeFormat('it-IT', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone,
  }).format(date)
  const time = new Intl.DateTimeFormat('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
    timeZone,
  }).format(date)
  return `${day} alle ${time}`
}

export type InvitationErrorCopy = {
  title: string
  hint: string
  retryable: boolean
}

// Il backend distingue gli esiti con un codice: la pagina mostra la via
// d'uscita giusta invece del solo status HTTP.
export function invitationErrorCopy(error: unknown): InvitationErrorCopy {
  const code = error instanceof ApiError ? error.code : ''
  const status = error instanceof ApiError ? error.status : 0
  if (code === 'invitation.expired')
    return {
      title: 'Questo invito è scaduto.',
      hint: 'I link valgono 72 ore. Chiedi all’organizzatore di reinviartelo: riceverai un nuovo link via email.',
      retryable: false,
    }
  if (code === 'invitation.revoked')
    return {
      title: 'Questo invito è stato revocato.',
      hint: 'L’organizzatore lo ha annullato. Chiedi un nuovo invito per entrare nella lega.',
      retryable: false,
    }
  if (code === 'invitation.consumed')
    return {
      title: 'Hai già accettato questo invito.',
      hint: 'Accedi con la tua email per ritrovare la lega.',
      retryable: false,
    }
  if (status === 404 || status === 410)
    return {
      title: 'Invito non trovato.',
      hint: 'Il link potrebbe essere incompleto. Riaprilo dall’email che hai ricevuto.',
      retryable: false,
    }
  return {
    title: 'Non riusciamo a verificare l’invito.',
    hint: 'Controlla la connessione e riprova tra poco.',
    retryable: true,
  }
}
