import { useId, useState } from 'react'
import { Icon } from '@/components/common/icon'
import { useInfiniteQuery } from '@tanstack/react-query'
import { Button } from '@/components/primitives/button'
import { rosterQueryOptions, loadedPlayers } from '../actions/auction.queries'
import type { AuctionTeam } from '../types/auction.types'

export function RecentPurchases({
  userId,
  sessionId,
  teams,
  onShowAll,
}: {
  userId: string
  sessionId: string
  teams: AuctionTeam[]
  onShowAll: () => void
}) {
  const [expanded, setExpanded] = useState(true)
  const contentId = useId()
  const result = useInfiniteQuery(rosterQueryOptions(userId, sessionId))
  // L’API ordina gli acquisti dal più recente; condividiamo la cache dello storico.
  const entries = loadedPlayers(result.data?.pages).slice(0, 3)
  return (
    <aside
      className="auction-recent-purchases"
      aria-label="Ultimi acquisti"
      data-expanded={expanded}
    >
      <header>
        <h2>
          <span className="recent-purchases-title">Ultimi acquisti</span>
          <button
            type="button"
            className="recent-purchases-toggle"
            aria-expanded={expanded}
            aria-controls={contentId}
            onClick={() => setExpanded((value) => !value)}
          >
            Ultimi acquisti
            <Icon name={expanded ? 'chevron-up' : 'chevron-down'} />
          </button>
        </h2>
      </header>
      <div id={contentId} className="recent-purchases-content">
        {result.isPending ? (
          <p className="recent-purchases-empty">Caricamento acquisti…</p>
        ) : result.isError && !result.data ? (
          <div className="recent-purchases-empty" role="alert">
            <p>Non riusciamo a caricare gli acquisti.</p>
            <Button variant="ghost" onClick={() => void result.refetch()}>
              Riprova
            </Button>
          </div>
        ) : entries.length ? (
          <ul>
            {entries.map((entry) => (
              <li key={entry.playerId}>
                <span className={`catalog-role role-${entry.role}`}>
                  {entry.role}
                </span>
                <div>
                  <h3 title={entry.name}>{entry.name}</h3>
                  <p>
                    <span aria-hidden="true">↳</span>
                    {teams.find((team) => team.id === entry.teamId)?.name ??
                      'Squadra'}
                  </p>
                </div>
                <strong
                  aria-label={`${entry.price} ${entry.price === 1 ? 'credito' : 'crediti'}`}
                >
                  {entry.price}
                </strong>
              </li>
            ))}
          </ul>
        ) : (
          <p className="recent-purchases-empty">
            Ancora nessun acquisto. Le aggiudicazioni appariranno qui.
          </p>
        )}
        <Button variant="ghost" onClick={onShowAll}>
          Visualizza tutto lo storico
        </Button>
      </div>
    </aside>
  )
}
