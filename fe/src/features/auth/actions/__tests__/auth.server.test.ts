// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resolveServerSession } from '../auth.server'

const context = vi.hoisted(() => ({
  getRequestHeader: vi.fn(),
  getResponseHeaders: vi.fn(),
}))
vi.mock('@tanstack/react-start/server', () => context)

const user = {
  id: 'user-1',
  email: 'member@example.test',
  displayName: 'Giulia',
  isSuperAdmin: false,
}
const respond = (data: unknown, status = 200, headers?: HeadersInit) =>
  new Response(
    JSON.stringify({ isSuccess: status === 200, data, errors: [] }),
    { status, headers },
  )

beforeEach(() => {
  vi.stubEnv('API_UPSTREAM', 'http://api.internal:6060')
  context.getRequestHeader.mockReset().mockReturnValue(undefined)
  context.getResponseHeaders.mockReturnValue(new Headers())
})
afterEach(() => {
  vi.unstubAllGlobals()
  vi.unstubAllEnvs()
})

describe('sessione SSR', () => {
  it('mostra il login senza interrogare il backend quando manca il cookie Identity', async () => {
    const fetch = vi.fn(() => Promise.reject(new Error('API spenta')))
    vi.stubGlobal('fetch', fetch)
    context.getRequestHeader.mockReturnValue('analytics=unrelated')
    expect(await resolveServerSession()).toBeNull()
    expect(fetch).not.toHaveBeenCalled()
  })

  it('verifica la sessione sul solo upstream configurato e inoltra i chunk Identity', async () => {
    context.getRequestHeader.mockReturnValue(
      'other=secret; Fantastiche.Auth=chunks-2; Fantastiche.AuthC1=first; Fantastiche.AuthC2=second',
    )
    const fetch = vi.fn(async () => respond(user))
    vi.stubGlobal('fetch', fetch)
    expect(await resolveServerSession()).toEqual(user)
    const [url, init] = fetch.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('http://api.internal:6060/api/Auth/Me')
    expect(new Headers(init.headers).get('cookie')).toBe(
      'Fantastiche.Auth=chunks-2; Fantastiche.AuthC1=first; Fantastiche.AuthC2=second',
    )
    expect(init.cache).toBe('no-store')
    expect(init.redirect).toBe('error')
  })

  it('propaga al browser i cookie Identity rinnovati dal backend durante SSR', async () => {
    context.getRequestHeader.mockReturnValue('Fantastiche.Auth=previous')
    const cookies = [
      'Fantastiche.Auth=chunks-2; Path=/; HttpOnly; Secure; SameSite=Lax; Expires=Sun, 20 Sep 2026 12:00:00 GMT',
      'Fantastiche.AuthC1=renewed-first; Path=/; HttpOnly; Secure',
      'Fantastiche.AuthC2=renewed-second; Path=/; HttpOnly; Secure',
    ]
    const headers = new Headers()
    for (const cookie of cookies) headers.append('Set-Cookie', cookie)
    headers.append('Set-Cookie', 'unrelated=private; Path=/')
    vi.stubGlobal('fetch', async () => respond(user, 200, headers))

    expect(await resolveServerSession()).toEqual(user)
    expect(context.getResponseHeaders().getSetCookie()).toEqual(cookies)
  })

  it('considera il cookie scaduto anonimo e propaga la cancellazione Identity', async () => {
    context.getRequestHeader.mockReturnValue('Fantastiche.Auth=expired')
    vi.stubGlobal('fetch', async () =>
      respond(null, 401, {
        'Set-Cookie': 'Fantastiche.Auth=; Max-Age=0; Path=/; HttpOnly',
      }),
    )
    expect(await resolveServerSession()).toBeNull()
    expect(context.getResponseHeaders().getSetCookie()).toEqual([
      'Fantastiche.Auth=; Max-Age=0; Path=/; HttpOnly',
    ])
  })

  it('non trasforma un errore di rete o backend in un logout', async () => {
    context.getRequestHeader.mockReturnValue('Fantastiche.Auth=valid')
    vi.stubGlobal('fetch', async () => {
      throw new TypeError('fetch failed')
    })
    await expect(resolveServerSession()).rejects.toMatchObject({
      code: 'network.unavailable',
    })
    vi.stubGlobal('fetch', async () => respond(null, 503))
    await expect(resolveServerSession()).rejects.toMatchObject({ status: 503 })
  })

  it('non condivide la sessione tra richieste concorrenti', async () => {
    context.getRequestHeader
      .mockReturnValueOnce('Fantastiche.Auth=alice')
      .mockReturnValueOnce('Fantastiche.Auth=bob')
    vi.stubGlobal('fetch', async (_url: string, init: RequestInit) => {
      const id = new Headers(init.headers).get('cookie')?.split('=')[1]
      await Promise.resolve()
      return respond({ ...user, id })
    })
    const [alice, bob] = await Promise.all([
      resolveServerSession(),
      resolveServerSession(),
    ])
    expect(alice?.id).toBe('alice')
    expect(bob?.id).toBe('bob')
  })
})
