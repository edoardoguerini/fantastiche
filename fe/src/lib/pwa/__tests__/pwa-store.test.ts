import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  dismissUpdate,
  getSnapshot,
  promptInstall,
  resetPwa,
  snoozeInstall,
  startPwa,
  subscribe,
} from '../pwa-store'

type Listener = (event: Event) => void

function fakeServiceWorker(waiting = false) {
  const listeners = new Map<string, Listener>()
  const registration = {
    waiting: waiting ? {} : null,
    installing: null as {
      addEventListener: (t: string, l: Listener) => void
    } | null,
    addEventListener: (type: string, listener: Listener) =>
      listeners.set(type, listener),
    removeEventListener: () => {},
    update: vi.fn(async () => {}),
  }
  const register = vi.fn(async () => registration)
  vi.stubGlobal('navigator', { ...navigator, serviceWorker: { register } })
  return { register, registration, listeners }
}

function installPromptEvent(outcome: 'accepted' | 'dismissed') {
  const event = new Event('beforeinstallprompt', {
    cancelable: true,
  }) as Event & {
    prompt: () => Promise<{ outcome: 'accepted' | 'dismissed' }>
  }
  event.prompt = vi.fn(async () => ({ outcome }))
  return event
}

afterEach(() => {
  resetPwa()
  vi.unstubAllGlobals()
  localStorage.clear()
})

describe('pwa store', () => {
  it('parte disabilitato e senza effetti lato server', () => {
    expect(getSnapshot()).toMatchObject({ enabled: false, updateReady: false })
  })

  it('registra il worker e segnala la versione in attesa', async () => {
    const { register, registration } = fakeServiceWorker(true)
    const changed = vi.fn()
    subscribe(changed)
    startPwa()
    expect(getSnapshot().enabled).toBe(true)
    expect(register).toHaveBeenCalledWith('/sw.js', {
      scope: '/',
      updateViaCache: 'none',
    })
    await vi.waitFor(() => expect(getSnapshot().updateReady).toBe(true))
    expect(changed).toHaveBeenCalled()
    dismissUpdate()
    expect(getSnapshot()).toMatchObject({
      updateReady: true,
      updateDismissed: true,
    })
    expect(registration.update).not.toHaveBeenCalled()
  })

  it('cattura il prompt nativo e lo consuma con Installa', async () => {
    fakeServiceWorker()
    startPwa()
    expect(await promptInstall()).toBe('unavailable')
    const event = installPromptEvent('accepted')
    window.dispatchEvent(event)
    expect(event.defaultPrevented).toBe(true)
    expect(getSnapshot().canPrompt).toBe(true)
    expect(await promptInstall()).toBe('accepted')
    expect(getSnapshot()).toMatchObject({
      canPrompt: false,
      installed: true,
      standalone: false,
    })
  })

  it('installare dalla scheda browser non la rende standalone', () => {
    fakeServiceWorker()
    startPwa()
    window.dispatchEvent(new Event('appinstalled'))
    expect(getSnapshot()).toMatchObject({
      installed: true,
      standalone: false,
      canPrompt: false,
    })
  })

  it('un prompt rifiutato lascia l’app non installata', async () => {
    fakeServiceWorker()
    startPwa()
    window.dispatchEvent(installPromptEvent('dismissed'))
    expect(await promptInstall()).toBe('dismissed')
    expect(getSnapshot()).toMatchObject({ canPrompt: false, standalone: false })
  })

  it('rimanda l’invito per trenta giorni sul dispositivo', () => {
    fakeServiceWorker()
    vi.useFakeTimers({ now: new Date('2026-09-13T20:00:00Z') })
    startPwa()
    expect(getSnapshot().installSnoozed).toBe(false)
    snoozeInstall()
    expect(getSnapshot().installSnoozed).toBe(true)
    resetPwa()
    vi.setSystemTime(new Date('2026-10-12T20:00:00Z'))
    startPwa()
    expect(getSnapshot().installSnoozed).toBe(true)
    resetPwa()
    vi.setSystemTime(new Date('2026-10-14T20:00:00Z'))
    startPwa()
    expect(getSnapshot().installSnoozed).toBe(false)
    vi.useRealTimers()
  })

  it('riconosce l’app già installata', () => {
    fakeServiceWorker()
    vi.stubGlobal('matchMedia', (query: string) => ({
      matches: query.includes('standalone'),
      addEventListener: () => {},
      removeEventListener: () => {},
    }))
    startPwa()
    expect(getSnapshot().standalone).toBe(true)
  })
})
