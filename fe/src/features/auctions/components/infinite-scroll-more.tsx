import { useEffect, useRef } from 'react'
import { Button } from '@/components/primitives/button'
import '../infinite-scroll.css'

export function InfiniteScrollMore({
  hasMore,
  fetching,
  error,
  onLoad,
  label,
}: {
  hasMore: boolean
  fetching: boolean
  error: boolean
  onLoad: () => void
  label: string
}) {
  const sentinel = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const element = sentinel.current
    if (!element || !hasMore || fetching || error) return
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry?.isIntersecting) {
          observer.disconnect()
          onLoad()
        }
      },
      { rootMargin: '0px 0px 120px 0px' },
    )
    observer.observe(element)
    return () => observer.disconnect()
  }, [hasMore, fetching, error, onLoad])

  if (!hasMore && !error) return null
  return (
    <div ref={sentinel} className="infinite-scroll-more" aria-busy={fetching}>
      {error && <p role="alert">Non riusciamo a caricare altri risultati.</p>}
      {fetching ? (
        <p role="status">Caricamento…</p>
      ) : (
        <Button
          variant="ghost"
          onClick={onLoad}
          aria-label={error ? `Riprova: ${label}` : label}
        >
          {error ? 'Riprova' : label}
        </Button>
      )}
    </div>
  )
}
