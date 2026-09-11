import { useState } from 'react'

export function LeagueLogo({
  name,
  url,
}: {
  name: string
  url?: string | null
}) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  return url && failedUrl !== url ? (
    <img
      className="league-logo"
      src={url}
      alt=""
      width={64}
      height={64}
      onError={() => setFailedUrl(url)}
    />
  ) : (
    <div className="league-monogram" aria-hidden="true">
      {name.slice(0, 1).toLocaleUpperCase('it')}
    </div>
  )
}
