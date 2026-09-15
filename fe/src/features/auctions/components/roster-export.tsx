import { useEffect, useRef, useState } from 'react'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { errorMessage } from '@/lib/api/error'
import { exportAuctionRoster } from '../actions/auction-export'

export function RosterExport({
  sessionId,
  completed,
}: {
  sessionId: string
  completed: boolean
}) {
  const request = useRef<AbortController | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [downloaded, setDownloaded] = useState(false)
  useEffect(() => () => request.current?.abort(), [])

  async function download() {
    if (request.current) return
    const controller = new AbortController()
    request.current = controller
    setPending(true)
    setError(null)
    setDownloaded(false)
    try {
      const file = await exportAuctionRoster(sessionId, controller.signal)
      if (controller.signal.aborted) return
      const url = URL.createObjectURL(
        new Blob([file.csv], { type: 'text/csv;charset=utf-8' }),
      )
      const link = document.createElement('a')
      link.href = url
      link.download = file.fileName
      document.body.append(link)
      try {
        link.click()
      } finally {
        link.remove()
        window.setTimeout(() => URL.revokeObjectURL(url), 1000)
      }
      setDownloaded(true)
    } catch (cause) {
      if (!controller.signal.aborted) setError(errorMessage(cause))
    } finally {
      if (!controller.signal.aborted) {
        request.current = null
        setPending(false)
      }
    }
  }

  return (
    <div className="roster-export">
      <div className="roster-export-heading">
        <div>
          <h2>Porta le rose su Fantacalcio.it</h2>
          <p>
            Scarica il CSV, poi caricalo in Gestione rose → Importa rose e
            associa le squadre.
          </p>
        </div>
        <Button
          variant="outline"
          onClick={() => void download()}
          disabled={pending}
        >
          <Icon name="cloud-arrow-down" />
          {pending ? 'Preparazione CSV…' : 'Esporta per Fantacalcio.it'}
        </Button>
      </div>
      {!completed && (
        <p>Durante l’asta il file contiene solo gli acquisti già conclusi.</p>
      )}
      <p>
        Su Leghe Fantacalcio verifica che stagione, budget e composizione delle
        rose corrispondano.
      </p>
      {error && (
        <p className="roster-export-error" role="alert">
          {error}
        </p>
      )}
      <p role="status">{downloaded ? 'CSV scaricato.' : ''}</p>
    </div>
  )
}
