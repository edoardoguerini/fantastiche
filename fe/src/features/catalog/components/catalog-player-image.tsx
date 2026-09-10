import { useState } from 'react'

export function CatalogPlayerImage({
  src,
  kind,
  fallback,
}: {
  src?: string | null
  kind: 'player' | 'club'
  fallback?: string
}) {
  const [failed, setFailed] = useState(false)
  return (
    <span className={`catalog-image catalog-image--${kind}`} aria-hidden="true">
      {src && !failed ? (
        <img
          src={src}
          alt=""
          loading="lazy"
          referrerPolicy="no-referrer"
          onError={() => setFailed(true)}
        />
      ) : (
        <span>{fallback ?? '—'}</span>
      )}
    </span>
  )
}
