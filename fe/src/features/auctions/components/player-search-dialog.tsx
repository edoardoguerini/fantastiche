import { useEffect, useState, type ComponentProps } from 'react'
import { Icon } from '@/components/common/icon'
import { CatalogPanel } from './catalog-panel'

export function PlayerSearchDialog({
  onClose,
  ...catalogProps
}: Omit<
  ComponentProps<typeof CatalogPanel>,
  'expanded' | 'active' | 'autoFocus' | 'portalContainer'
> & {
  onClose: () => void
}) {
  const [dialog, setDialog] = useState<HTMLDialogElement | null>(null)
  useEffect(() => {
    if (!dialog) return
    const previous = document.activeElement
    const overflow = document.body.style.overflow
    dialog.showModal()
    document.body.style.overflow = 'hidden'
    dialog.querySelector('input')?.focus()
    return () => {
      dialog.close()
      document.body.style.overflow = overflow
      if (previous instanceof HTMLElement && previous.isConnected)
        previous.focus({ preventScroll: true })
    }
  }, [dialog])
  return (
    <dialog
      ref={setDialog}
      className="auction-player-search-dialog"
      aria-labelledby="player-search-dialog-title"
      onCancel={(event) => {
        event.preventDefault()
        onClose()
      }}
      onClose={onClose}
    >
      <header>
        <div>
          <h2 id="player-search-dialog-title">Scegli un calciatore</h2>
          <p>Cerca nel listone e seleziona chi chiamare.</p>
        </div>
        <button type="button" aria-label="Chiudi ricerca" onClick={onClose}>
          <Icon name="xmark" />
        </button>
      </header>
      <CatalogPanel
        {...catalogProps}
        expanded
        autoFocus
        portalContainer={dialog}
      />
    </dialog>
  )
}
