import { useId } from 'react'
import { Link } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import type { League } from '../types/leagues.types'
import { AuctionStatus } from './auction-status'
import { LeagueLogo } from './league-logo'

export function LeagueCard({ league }: { league: League }) {
  const headingId = useId()
  const rosterSize =
    league.goalkeepers + league.defenders + league.midfielders + league.forwards
  return (
    <article className="league-card" aria-labelledby={headingId}>
      <LeagueLogo name={league.name} url={league.logoUrl} />
      <div className="league-card-content">
        <div className="league-card-meta">
          <p className="league-season">Stagione {league.seasonName}</p>
          {league.auctionStatus && (
            <AuctionStatus status={league.auctionStatus} />
          )}
        </div>
        <h2 id={headingId}>{league.name}</h2>
        {league.myTeamName && (
          <p className="league-my-team">{league.myTeamName}</p>
        )}
        <p className="league-card-rules">
          <span>Budget iniziale {league.budget}</span>
          <span aria-hidden="true">·</span>
          <span>Rosa da {rosterSize}</span>
        </p>
      </div>
      <div className="league-card-actions">
        <Button asChild>
          <Link to="/leghe/$leagueId/asta" params={{ leagueId: league.id }}>
            {league.auctionStatus === 'Completed'
              ? 'Rivedi l’asta'
              : 'Entra nell’asta'}
          </Link>
        </Button>
        <Button variant="outline" asChild>
          <Link
            to="/leghe/$leagueId"
            params={{ leagueId: league.id }}
            aria-label={`Dettagli lega ${league.name}`}
          >
            Dettagli lega
          </Link>
        </Button>
      </div>
    </article>
  )
}
