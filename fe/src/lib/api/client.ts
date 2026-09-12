import { parseApiResponse } from './response'
import { ApiError } from './error'

export const apiBaseUrl = (
  import.meta.env.VITE_API_BASE_URL ??
  (import.meta.env.DEV ? 'http://localhost:6060' : '')
).replace(/\/$/, '')

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
  return parseApiResponse<T>(response)
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
