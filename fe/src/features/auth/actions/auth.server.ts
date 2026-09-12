import {
  getRequestHeader,
  getResponseHeaders,
} from '@tanstack/react-start/server'
import { ApiError } from '@/lib/api/error'
import { parseApiResponse } from '@/lib/api/response'
import { authenticatedUserSchema } from '../types/auth.types'

export async function resolveServerSession(signal?: AbortSignal) {
  // Identity può dividere il ticket in più cookie. Nessun cookie estraneo
  // all'autenticazione deve raggiungere il backend attraverso il rendering.
  const cookie = (getRequestHeader('cookie') ?? '')
    .split(';')
    .map((part) => part.trim())
    .filter((part) => /^Fantastiche\.Auth(?:C\d+)?=/.test(part))
    .join('; ')
  if (!cookie) return null

  const upstream = process.env.API_UPSTREAM ?? 'http://localhost:6060'
  let response: Response
  try {
    response = await fetch(`${upstream.replace(/\/$/, '')}/api/Auth/Me`, {
      headers: { Cookie: cookie },
      cache: 'no-store',
      redirect: 'error',
      signal: signal
        ? AbortSignal.any([signal, AbortSignal.timeout(10_000)])
        : AbortSignal.timeout(10_000),
    })
  } catch (error) {
    if (signal?.aborted) throw error
    throw new ApiError(
      0,
      'network.unavailable',
      'Impossibile contattare il servizio. Controlla la connessione e riprova.',
    )
  }
  for (const value of response.headers.getSetCookie()) {
    if (/^Fantastiche\.Auth(?:C\d+)?=/.test(value))
      getResponseHeaders().append('Set-Cookie', value)
  }
  if (response.status === 401) return null
  return authenticatedUserSchema.parse(await parseApiResponse(response))
}
