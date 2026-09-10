export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly data?: unknown,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

export function errorMessage(error: unknown): string {
  if (!(error instanceof ApiError))
    return 'Qualcosa non ha funzionato. Riprova tra poco.'
  if (error.code === 'security.antiforgery')
    return 'La sessione è cambiata. Riprova a inviare il modulo.'
  if (error.status === 429)
    return 'Troppi tentativi. Attendi qualche minuto prima di riprovare.'
  if (error.status === 401)
    return 'Email o password non corrette, oppure sessione scaduta. Riprova ad accedere.'
  if (error.status === 403) return 'Non hai accesso a questa risorsa.'
  if (error.status >= 500)
    return 'Il servizio non è disponibile al momento. Riprova tra poco.'
  return error.message
}
