import { z } from 'zod'
import { ApiError } from './error'

export const apiBaseUrl = (
  import.meta.env.VITE_API_BASE_URL ??
  (import.meta.env.DEV ? 'http://localhost:6060' : '')
).replace(/\/$/, '')
const envelopeSchema = z.object({
  isSuccess: z.boolean(),
  data: z.unknown(),
  errors: z.array(z.object({ code: z.string(), message: z.string() })),
})

async function request<T>(path: string, init: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}/api${path}`, {
      ...init,
      credentials: 'include',
      cache: 'no-store',
      referrerPolicy: 'no-referrer',
    })
  } catch (error) {
    if (init.signal?.aborted) throw error
    throw new ApiError(
      0,
      'network.unavailable',
      'Impossibile contattare il servizio. Controlla la connessione e riprova.',
    )
  }
  const parsed = envelopeSchema.safeParse(
    await response.json().catch(() => null),
  )
  if (!response.ok || !parsed.success || !parsed.data.isSuccess) {
    const firstError = parsed.success ? parsed.data.errors[0] : undefined
    throw new ApiError(
      response.status,
      firstError?.code ?? 'api.invalid_response',
      firstError?.message ??
        'Il servizio ha restituito una risposta inattesa. Riprova tra poco.',
      parsed.success ? parsed.data.data : undefined,
    )
  }
  return parsed.data.data as T
}

async function mutate<T>(
  method: 'POST' | 'PUT',
  path: string,
  body: unknown,
  signal?: AbortSignal,
): Promise<T> {
  // Il token è legato all'identità corrente: si richiede nuovamente a ogni mutazione.
  const { token } = await request<{ token: string }>('/Auth/Antiforgery', {
    method: 'GET',
    signal,
  })
  return request<T>(path, {
    method,
    signal,
    headers: { 'Content-Type': 'application/json', 'X-XSRF-TOKEN': token },
    body: JSON.stringify(body),
  })
}

export const api = {
  get: <T>(path: string, signal?: AbortSignal, headers?: HeadersInit) =>
    request<T>(path, { method: 'GET', signal, headers }),
  post: <T>(path: string, body: unknown, signal?: AbortSignal) =>
    mutate<T>('POST', path, body, signal),
  put: <T>(path: string, body: unknown, signal?: AbortSignal) =>
    mutate<T>('PUT', path, body, signal),
}
