import { useEffect, useState } from 'react'
import { remainingSeconds } from '../validations/auction-rules'
import type { TimedSession } from '../types/auction.types'

export function useAuctionClock(session: TimedSession) {
  const [now, setNow] = useState(() => performance.now())
  useEffect(() => {
    const timer = setInterval(() => setNow(performance.now()), 100)
    return () => clearInterval(timer)
  }, [])
  const auction = session.currentAuction
  return auction?.status === 'Open'
    ? remainingSeconds(
        auction.deadline,
        session.serverTime,
        session.receivedAt,
        now,
      )
    : 0
}
