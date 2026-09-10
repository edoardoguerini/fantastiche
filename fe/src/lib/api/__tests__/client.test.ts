import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../client'

afterEach(() => vi.unstubAllGlobals())
const success = (data: unknown) =>
  new Response(JSON.stringify({ isSuccess: true, data, errors: [] }))

describe('trasporto API', () => {
  it('invia il token invito solo nell’header e protegge PUT con antiforgery', async () => {
    const requests: Array<{ url: string; init?: RequestInit }> = []
    vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
      requests.push({ url, init })
      return success(url.endsWith('/Antiforgery') ? { token: 'csrf-put' } : {})
    })
    await api.get('/Invitations/Preview', undefined, {
      'X-Invitation-Token': 'private-invite',
    })
    await api.put('/Leagues/league/Seasons/season/Catalog', {
      listVersionId: 'list',
    })
    expect(requests[0]?.url).not.toContain('private-invite')
    expect(
      new Headers(requests[0]?.init?.headers).get('X-Invitation-Token'),
    ).toBe('private-invite')
    expect(requests[0]?.init?.referrerPolicy).toBe('no-referrer')
    expect(requests[2]?.init?.method).toBe('PUT')
    expect(new Headers(requests[2]?.init?.headers).get('X-XSRF-TOKEN')).toBe(
      'csrf-put',
    )
    expect(requests[2]?.init?.body).toBe(
      JSON.stringify({ listVersionId: 'list' }),
    )
  })
  it('invia cookie e antiforgery prima della mutazione, senza conservare il token fra sessioni', async () => {
    const requests: Array<{ url: string; init?: RequestInit }> = []
    let token = 0
    vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
      requests.push({ url, init })
      return url.endsWith('/Antiforgery')
        ? success({ token: `token-${++token}` })
        : success({ signedOut: true })
    })
    await api.post('/Auth/Logout', {})
    await api.post('/Auth/Login', {
      email: 'test@example.test',
      password: 'test-password',
    })
    expect(requests.map((request) => request.url.split('/api')[1])).toEqual([
      '/Auth/Antiforgery',
      '/Auth/Logout',
      '/Auth/Antiforgery',
      '/Auth/Login',
    ])
    expect(
      requests.every((request) => request.init?.credentials === 'include'),
    ).toBe(true)
    expect(new Headers(requests[1]?.init?.headers).get('X-XSRF-TOKEN')).toBe(
      'token-1',
    )
    expect(new Headers(requests[3]?.init?.headers).get('X-XSRF-TOKEN')).toBe(
      'token-2',
    )
  })

  it('mantiene status e codice degli errori applicativi senza ritentare una mutazione', async () => {
    let writes = 0
    vi.stubGlobal('fetch', async (url: string) => {
      if (url.endsWith('/Antiforgery')) return success({ token: 'token' })
      writes++
      return new Response(
        JSON.stringify({
          isSuccess: false,
          data: null,
          errors: [
            {
              code: 'auth.invalid_credentials',
              message: 'Credenziali non valide.',
            },
          ],
        }),
        { status: 401 },
      )
    })
    await expect(api.post('/Auth/Login', {})).rejects.toMatchObject({
      status: 401,
      code: 'auth.invalid_credentials',
    })
    expect(writes).toBe(1)
  })

  it('rende comprensibile una risposta proxy non JSON', async () => {
    vi.stubGlobal(
      'fetch',
      async () => new Response('<html>Unavailable</html>', { status: 502 }),
    )
    await expect(api.get('/Leagues')).rejects.toMatchObject({ status: 502 })
  })

  it('distingue rete non disponibile da sessione scaduta', async () => {
    vi.stubGlobal('fetch', async () => {
      throw new TypeError('Failed to fetch')
    })
    await expect(api.get('/Auth/Me')).rejects.toMatchObject({
      status: 0,
      code: 'network.unavailable',
    })
  })

  it('propaga la cancellazione per evitare risposte di una sessione precedente', async () => {
    const controller = new AbortController()
    controller.abort()
    vi.stubGlobal('fetch', async (_url: string, init?: RequestInit) => {
      init?.signal?.throwIfAborted()
    })
    await expect(api.get('/Leagues', controller.signal)).rejects.toMatchObject({
      name: 'AbortError',
    })
  })
})
