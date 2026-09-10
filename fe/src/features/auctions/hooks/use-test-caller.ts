import { useEffect, useRef, useState } from 'react'
import type { AuctionSession } from '../types/auction.types'

/** Aiuto esplicito solo in sviluppo; usa gli stessi comandi e permessi dell’asta. */
export function useTestCaller({
  enabledInitially,
  session,
  myTeamId,
  canManage,
  blocked,
  send,
}: {
  enabledInitially: boolean
  session: AuctionSession
  myTeamId: string | null
  canManage: boolean
  blocked: boolean
  send: (kind: 'Control', body: Record<string, unknown>) => Promise<void>
}) {
  const [enabled, setEnabled] = useState(
    import.meta.env.DEV && enabledInitially,
  )
  const attempted = useRef<string | null>(null)
  useEffect(() => {
    if (
      !import.meta.env.DEV ||
      !enabled ||
      !canManage ||
      !myTeamId ||
      blocked ||
      session.status !== 'Active' ||
      session.currentAuction?.status === 'Open' ||
      session.currentTeamId === myTeamId
    )
      return
    const key = `${session.id}:${session.version}:${myTeamId}`
    if (attempted.current === key) return
    attempted.current = key
    void send('Control', { action: 'GoToTurn', targetTeamId: myTeamId })
  }, [enabled, canManage, myTeamId, blocked, session, send])
  return { enabled, setEnabled }
}
