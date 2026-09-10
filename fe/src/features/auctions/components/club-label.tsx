import { useState } from 'react'

export function ClubLabel({
  name,
  logoUrl,
}: {
  name: string
  logoUrl?: string | null
}) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  return (
    <span className="club-label">
      {logoUrl && failedUrl !== logoUrl && (
        <img
          src={logoUrl}
          alt=""
          loading="lazy"
          decoding="async"
          onError={() => setFailedUrl(logoUrl)}
        />
      )}
      <span>{name}</span>
    </span>
  )
}
