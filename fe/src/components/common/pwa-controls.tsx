import { useEffect, useState } from 'react'
import { useRouterState } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import {
  dismissUpdate,
  promptInstall,
  snoozeInstall,
  startPwa,
} from '@/lib/pwa/pwa-store'
import { usePwa } from '@/lib/pwa/use-pwa'
import { Icon } from './icon'
import { PwaInstallGuide } from './pwa-install-guide'
import './pwa-controls.css'

/** Montato nel root in produzione: avvia il supporto PWA e mostra gli avvisi. */
export function PwaControls() {
  const pathname = useRouterState({
    select: (state) => state.location.pathname,
  })
  useEffect(() => {
    startPwa()
  }, [])
  return <PwaSurfaces pathname={pathname} />
}

export function PwaSurfaces({ pathname }: { pathname: string }) {
  const pwa = usePwa()
  if (!pwa.enabled) return null
  // Nessun avviso sovrapposto ai comandi d'asta o all'attivazione account.
  if (/\/asta(?:\/|$)/.test(pathname) || pathname.startsWith('/invito'))
    return null
  if (pwa.standalone && pwa.updateReady)
    return pwa.updateDismissed ? null : <UpdateToast />
  if (
    pathname === '/login' &&
    !pwa.standalone &&
    !pwa.installed &&
    !pwa.installSnoozed
  )
    return <InstallCard canPrompt={pwa.canPrompt} />
  return null
}

function UpdateToast() {
  return (
    <div className="pwa-toast" role="status">
      <span className="pwa-icon">
        <Icon name="arrows-rotate" />
      </span>
      <div className="pwa-body">
        <p className="pwa-title">Nuova versione pronta</p>
        <p>Dopo l’asta chiudi tutte le finestre di Fantastiche e riaprila.</p>
      </div>
      <button
        type="button"
        className="pwa-close"
        aria-label="Chiudi avviso"
        onClick={dismissUpdate}
      >
        <Icon name="xmark" />
      </button>
    </div>
  )
}

function InstallCard({ canPrompt }: { canPrompt: boolean }) {
  const [guide, setGuide] = useState(false)
  async function install() {
    if (!canPrompt || (await promptInstall()) === 'unavailable') setGuide(true)
  }
  return (
    <section className="pwa-install" aria-label="Installa Fantastiche">
      <div className="pwa-install-head">
        <img
          className="pwa-install-logo"
          src="/brand/fantastiche-logo.png"
          alt=""
          width="1170"
          height="1170"
        />
        <div className="pwa-body">
          <p className="pwa-title">Porta Fantastiche sulla Home</p>
          {guide ? (
            <PwaInstallGuide />
          ) : (
            <p>
              Si apre a tutto schermo, senza barra del browser, ed è pronta per
              l’asta.
            </p>
          )}
        </div>
      </div>
      <div className="pwa-install-actions">
        {guide ? (
          <Button variant="ghost" onClick={snoozeInstall}>
            Chiudi
          </Button>
        ) : (
          <>
            <Button variant="ghost" onClick={snoozeInstall}>
              Non ora
            </Button>
            <Button onClick={() => void install()}>
              <Icon name="cloud-arrow-down" /> Installa
            </Button>
          </>
        )}
      </div>
    </section>
  )
}
