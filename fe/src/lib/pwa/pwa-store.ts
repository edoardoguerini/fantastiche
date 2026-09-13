// Stato PWA condiviso da avvisi e menu account. Nessun skipWaiting o reload:
// il nuovo worker si attiva solo quando tutte le finestre sono chiuse.

export type PwaState = {
  /** Vero solo nel browser, dopo l'avvio in produzione. */
  enabled: boolean
  /** Finestra corrente aperta come PWA. */
  standalone: boolean
  /** Installazione rilevata durante questa apertura del browser. */
  installed: boolean
  /** Il browser ha offerto un prompt d'installazione nativo. */
  canPrompt: boolean
  /** Un nuovo worker attende la chiusura delle finestre. */
  updateReady: boolean
  /** L'avviso di aggiornamento è stato chiuso per questa apertura. */
  updateDismissed: boolean
  /** L'invito all'installazione è stato rimandato su questo dispositivo. */
  installSnoozed: boolean
}

export type InstallOutcome = 'accepted' | 'dismissed' | 'unavailable'

type InstallPromptEvent = Event & {
  prompt: () => Promise<{ outcome: 'accepted' | 'dismissed' }>
}

const SNOOZE_KEY = 'fantastiche.install-snoozed-until'
const SNOOZE_MS = 30 * 24 * 60 * 60 * 1000
const UPDATE_CHECK_MS = 60_000

const initial: PwaState = {
  enabled: false,
  standalone: false,
  installed: false,
  canPrompt: false,
  updateReady: false,
  updateDismissed: false,
  installSnoozed: false,
}

let state = initial
let prompt: InstallPromptEvent | null = null
let stop: (() => void) | null = null
const listeners = new Set<() => void>()

function set(patch: Partial<PwaState>) {
  state = { ...state, ...patch }
  listeners.forEach((listener) => listener())
}

export function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function getSnapshot() {
  return state
}

export function getServerSnapshot() {
  return initial
}

function standaloneMedia() {
  return typeof window.matchMedia === 'function'
    ? window.matchMedia('(display-mode: standalone)')
    : null
}

function isStandalone() {
  return (
    standaloneMedia()?.matches === true ||
    (navigator as Navigator & { standalone?: boolean }).standalone === true
  )
}

function isSnoozed() {
  try {
    const until = Number(localStorage.getItem(SNOOZE_KEY))
    return Number.isFinite(until) && until > Date.now()
  } catch {
    return false
  }
}

function watchServiceWorker() {
  if (!('serviceWorker' in navigator)) return () => {}
  let disposed = false
  let cleanup = () => {}
  void navigator.serviceWorker
    .register('/sw.js', { scope: '/', updateViaCache: 'none' })
    .then((registration) => {
      if (disposed) return
      const pending = new Set<ServiceWorker>()
      const check = () => {
        if (!disposed) set({ updateReady: !!registration.waiting })
      }
      const found = () => {
        const worker = registration.installing
        if (worker) {
          pending.add(worker)
          worker.addEventListener('statechange', check)
        }
      }
      let lastCheck = Date.now()
      const checkOnReturn = () => {
        if (
          document.visibilityState !== 'visible' ||
          Date.now() - lastCheck < UPDATE_CHECK_MS
        )
          return
        lastCheck = Date.now()
        void registration.update().catch(() => {})
      }
      check()
      found()
      registration.addEventListener('updatefound', found)
      document.addEventListener('visibilitychange', checkOnReturn)
      cleanup = () => {
        registration.removeEventListener('updatefound', found)
        document.removeEventListener('visibilitychange', checkOnReturn)
        pending.forEach((worker) =>
          worker.removeEventListener('statechange', check),
        )
      }
    })
    .catch(() => {
      /* L'app online resta utilizzabile se il browser blocca il worker. */
    })
  return () => {
    disposed = true
    cleanup()
  }
}

/** Avvia il supporto PWA nel browser. Idempotente. */
export function startPwa() {
  if (stop) return stop
  const media = standaloneMedia()
  const onDisplayMode = () => set({ standalone: isStandalone() })
  const onPrompt = (event: Event) => {
    event.preventDefault()
    prompt = event as InstallPromptEvent
    set({ canPrompt: true })
  }
  const onInstalled = () => {
    prompt = null
    set({ installed: true, canPrompt: false })
  }
  window.addEventListener('beforeinstallprompt', onPrompt)
  window.addEventListener('appinstalled', onInstalled)
  media?.addEventListener('change', onDisplayMode)
  set({
    enabled: true,
    standalone: isStandalone(),
    installSnoozed: isSnoozed(),
  })
  const stopWorker = watchServiceWorker()
  stop = () => {
    window.removeEventListener('beforeinstallprompt', onPrompt)
    window.removeEventListener('appinstalled', onInstalled)
    media?.removeEventListener('change', onDisplayMode)
    stopWorker()
    stop = null
  }
  return stop
}

/** Ferma il supporto e riporta lo stato iniziale (test). */
export function resetPwa() {
  stop?.()
  prompt = null
  state = initial
  listeners.forEach((listener) => listener())
}

/** Apre il prompt nativo, se il browser lo ha offerto. */
export async function promptInstall(): Promise<InstallOutcome> {
  const current = prompt
  if (!current) return 'unavailable'
  prompt = null
  set({ canPrompt: false })
  try {
    const { outcome } = await current.prompt()
    if (outcome === 'accepted') set({ installed: true })
    return outcome
  } catch {
    return 'unavailable'
  }
}

export function dismissUpdate() {
  set({ updateDismissed: true })
}

export function snoozeInstall() {
  try {
    localStorage.setItem(SNOOZE_KEY, String(Date.now() + SNOOZE_MS))
  } catch {
    /* Senza storage l'invito torna alla prossima apertura. */
  }
  set({ installSnoozed: true })
}
