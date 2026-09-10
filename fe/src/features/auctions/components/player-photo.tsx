import { useState } from 'react'
import type { Role } from '../types/auction.types'

export function PlayerPhoto({
  url,
  role,
  large = false,
}: {
  url?: string | null
  role: Role
  large?: boolean
}) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  if (!url || failedUrl === url)
    return <span className={`role-badge role-${role}`}>{role}</span>
  return (
    <span className={`player-photo ${large ? 'player-photo--large' : ''}`}>
      <img
        src={url}
        alt=""
        loading={large ? 'eager' : 'lazy'}
        decoding="async"
        onError={() => setFailedUrl(url)}
      />
    </span>
  )
}
