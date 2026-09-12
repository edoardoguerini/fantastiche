import type { League } from '../types/leagues.types'

const auctionLabels = {
  NotStarted: 'Da iniziare',
  Active: 'In corso',
  Paused: 'In pausa',
  Completed: 'Conclusa',
} as const

export function AuctionStatus({
  status,
}: {
  status: NonNullable<League['auctionStatus']>
}) {
  return (
    <span className="league-auction-status" data-status={status}>
      <span aria-hidden="true" />
      {auctionLabels[status]}
    </span>
  )
}
