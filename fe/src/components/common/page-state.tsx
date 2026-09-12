import { useHydrated } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import { errorMessage } from '@/lib/api/error'

export function LoadingState({
  message = 'Caricamento in corso…',
}: {
  message?: string
}) {
  return (
    <div className="page-state" role="status">
      <span className="loading-dot" aria-hidden="true" />
      <p>{message}</p>
    </div>
  )
}

export function ErrorState({
  error,
  retry,
}: {
  error: unknown
  retry: () => void
}) {
  const hydrated = useHydrated()
  return (
    <div className="page-state">
      <h2>Non riusciamo a caricare questa pagina</h2>
      <p role="alert">{errorMessage(error)}</p>
      <Button variant="outline" onClick={retry} disabled={!hydrated}>
        Riprova
      </Button>
    </div>
  )
}
