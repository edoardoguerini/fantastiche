import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { LoginForm } from '../login-form'

afterEach(() => vi.unstubAllGlobals())
const account = {
  id: 'account-1',
  email: 'test@example.test',
  displayName: 'Test',
  isSuperAdmin: false,
}
const response = (data: unknown) =>
  new Response(JSON.stringify({ isSuccess: true, data, errors: [] }))
function setup() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const onSuccess = vi.fn()
  render(
    <QueryClientProvider client={client}>
      <LoginForm onSuccess={onSuccess} />
    </QueryClientProvider>,
  )
  return { user: userEvent.setup(), client, onSuccess }
}

describe('login', () => {
  it('valida i campi e permette di mostrare la password senza inviare il form', async () => {
    let requests = 0
    vi.stubGlobal('fetch', async () => {
      requests++
      return response(account)
    })
    const { user } = setup()
    await user.click(screen.getByRole('button', { name: 'Accedi' }))
    expect(await screen.findAllByRole('alert')).not.toHaveLength(0)
    expect(requests).toBe(0)
    await user.type(
      screen.getByLabelText('Password', { exact: true }),
      'una-password',
    )
    await user.click(screen.getByRole('button', { name: 'Mostra password' }))
    expect(screen.getByLabelText('Password', { exact: true })).toHaveAttribute(
      'type',
      'text',
    )
    expect(requests).toBe(0)
  })

  it('mostra il rifiuto del server e consente di riprovare', async () => {
    vi.stubGlobal('fetch', async (url: string) =>
      url.endsWith('/Antiforgery')
        ? response({ token: 'csrf' })
        : new Response(
            JSON.stringify({
              isSuccess: false,
              data: null,
              errors: [
                {
                  code: 'auth.invalid_credentials',
                  message: 'Email o password non corrette.',
                },
              ],
            }),
            { status: 401 },
          ),
    )
    const { user, onSuccess } = setup()
    await user.type(screen.getByLabelText('Email'), account.email)
    await user.type(
      screen.getByLabelText('Password', { exact: true }),
      'wrong-password',
    )
    await user.click(screen.getByRole('button', { name: 'Accedi' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(
      /Email o password/,
    )
    expect(screen.getByRole('button', { name: 'Accedi' })).toBeEnabled()
    expect(onSuccess).not.toHaveBeenCalled()
  })

  it('blocca doppi invii e rimuove i dati del precedente account prima di entrare', async () => {
    let resolveLogin: ((value: Response) => void) | undefined
    let writes = 0
    vi.stubGlobal('fetch', async (url: string) => {
      if (url.endsWith('/Antiforgery')) return response({ token: 'csrf' })
      writes++
      return new Promise<Response>((resolve) => {
        resolveLogin = resolve
      })
    })
    const { user, client, onSuccess } = setup()
    client.setQueryData(['leagues', 'old-user'], ['private-data'])
    await user.type(screen.getByLabelText('Email'), account.email)
    await user.type(
      screen.getByLabelText('Password', { exact: true }),
      'correct-password',
    )
    await user.dblClick(screen.getByRole('button', { name: 'Accedi' }))
    expect(
      screen.getByRole('button', { name: /Accesso in corso/ }),
    ).toBeDisabled()
    expect(writes).toBe(1)
    await act(async () => resolveLogin?.(response(account)))
    await waitFor(() => expect(onSuccess).toHaveBeenCalledOnce())
    expect(client.getQueryData(['leagues', 'old-user'])).toBeUndefined()
  })

  it('precompila e blocca l’email quando la pagina la conosce già', async () => {
    let body: Record<string, unknown> | undefined
    vi.stubGlobal('fetch', async (url: string, init?: RequestInit) => {
      if (url.endsWith('/Antiforgery')) return response({ token: 'csrf' })
      body = JSON.parse(String(init?.body)) as Record<string, unknown>
      return response(account)
    })
    const client = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    })
    const onSuccess = vi.fn()
    render(
      <QueryClientProvider client={client}>
        <LoginForm
          onSuccess={onSuccess}
          initialEmail={account.email}
          lockEmail
        />
      </QueryClientProvider>,
    )
    const email = screen.getByLabelText('Email')
    expect(email).toHaveValue(account.email)
    expect(email).toHaveAttribute('readonly')
    expect(screen.getByLabelText('Password', { exact: true })).toHaveFocus()
    const user = userEvent.setup()
    await user.keyboard('una-password')
    await user.click(screen.getByRole('button', { name: 'Accedi' }))
    await waitFor(() => expect(onSuccess).toHaveBeenCalledOnce())
    expect(body).toMatchObject({ email: account.email })
  })
})
