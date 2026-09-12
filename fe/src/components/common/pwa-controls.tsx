import { useEffect, useState, useSyncExternalStore } from 'react'
import { useRouterState } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import { Icon } from './icon'
import './pwa-controls.css'

type InstallPrompt = Event & {
  prompt: () => Promise<{ outcome: 'accepted' | 'dismissed' }>
}

function installed() {
  return (
    window.matchMedia('(display-mode: standalone)').matches ||
    (navigator as Navigator & { standalone?: boolean }).standalone === true
  )
}
function subscribeInstalled(notify: () => void) {
  const media = window.matchMedia('(display-mode: standalone)')
  media.addEventListener('change', notify)
  window.addEventListener('appinstalled', notify)
  return () => {
    media.removeEventListener('change', notify)
    window.removeEventListener('appinstalled', notify)
  }
}

export function PwaControls() {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  })
  const standalone = useSyncExternalStore(
    subscribeInstalled,
    installed,
    () => true,
  )
  const [prompt, setPrompt] = useState<InstallPrompt | null>(null)
  const [didInstall, setDidInstall] = useState(false)
  const [updateReady, setUpdateReady] = useState(false)

  useEffect(() => {
    function available(event: Event) {
      event.preventDefault()
      setPrompt(event as InstallPrompt)
    }
    function completed() {
      setDidInstall(true)
      setPrompt(null)
    }
    window.addEventListener('beforeinstallprompt', available)
    window.addEventListener('appinstalled', completed)
    return () => {
      window.removeEventListener('beforeinstallprompt', available)
      window.removeEventListener('appinstalled', completed)
    }
  }, [])

  useEffect(() => {
    if (!('serviceWorker' in navigator)) return
    let disposed = false
    let cleanup = () => {}
    void navigator.serviceWorker
      .register('/sw.js', { scope: '/', updateViaCache: 'none' })
      .then((registration) => {
        if (disposed) return
        const pending = new Set<ServiceWorker>()
        const check = () => {
          if (!disposed) setUpdateReady(!!registration.waiting)
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
            Date.now() - lastCheck < 60_000
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
  }, [])

  // Nessun invito o avviso sovrapposto ai comandi d'asta o all'attivazione account.
  if (/\/asta(?:\/|$)/.test(pathname) || pathname.startsWith('/invito'))
    return null
  if ((standalone || didInstall) && !updateReady) return null
  return (
    <aside className="pwa-controls" aria-label="App Fantastiche">
      {updateReady && (
        <p role="status">
          Aggiornamento pronto. Dopo l’asta, chiudi tutte le finestre di
          Fantastiche e riapri l’app per applicarlo.
        </p>
      )}
      {!standalone &&
        !didInstall &&
        (prompt ? (
          <Button
            variant="ghost"
            onClick={async () => {
              const current = prompt
              setPrompt(null)
              try {
                const result = await current.prompt()
                if (result.outcome === 'accepted') setDidInstall(true)
              } catch {
                /* La guida manuale rimane disponibile. */
              }
            }}
          >
            <Icon name="download" /> Installa app
          </Button>
        ) : (
          <details>
            <summary>
              <Icon name="download" /> Installa app
            </summary>
            <p>
              Su iPhone e iPad: apri Condividi e scegli «Aggiungi alla schermata
              Home».
            </p>
            <p>
              Su Android e computer: usa «Installa app» nel menu del browser. Su
              Safari per Mac: File → Aggiungi al Dock.
            </p>
          </details>
        ))}
    </aside>
  )
}
