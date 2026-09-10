import { PlayerValuation } from './player-valuation'
import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Input } from '@/components/primitives/input'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { catalogQueryOptions } from '../actions/auction.queries'
import {
  roles,
  type AuctionTeam,
  type CatalogEntry,
  type RosterRules,
  type Role,
} from '../types/auction.types'
import { canBuyRole } from '../validations/auction-rules'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'

export function CatalogPanel({
  userId,
  sessionId,
  canCall,
  currentRole,
  team,
  rules,
  onSelect,
  selectedPlayerId,
}: {
  userId: string
  sessionId: string
  canCall: boolean
  currentRole: Role | null
  team?: AuctionTeam
  rules: RosterRules
  onSelect: (player: CatalogEntry) => void
  selectedPlayerId?: string
}) {
  const [search, setSearch] = useState('')
  const [debounced, setDebounced] = useState('')
  const [role, setRole] = useState<string>(currentRole ?? '')
  const [page, setPage] = useState(1)
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebounced(search)
      setPage(1)
    }, 250)
    return () => clearTimeout(timer)
  }, [search])
  const result = useQuery(
    catalogQueryOptions(userId, sessionId, debounced, role, page),
  )
  return (
    <div className="auction-catalog">
      <div className="catalog-toolbar">
        <div className="catalog-search">
          <Icon name="magnifying-glass" />
          <label className="sr-only" htmlFor="player-search">
            Cerca calciatore
          </label>
          <Input
            id="player-search"
            placeholder="Cerca un calciatore…"
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>
        <div className="role-filters" aria-label="Filtra per ruolo">
          {[{ id: '', label: 'Tutti' }, ...roles].map((item) => (
            <button
              key={item.id}
              type="button"
              data-role={item.id}
              aria-pressed={role === item.id}
              onClick={() => {
                setRole(item.id)
                setPage(1)
              }}
            >
              {item.id || item.label}
            </button>
          ))}
        </div>
      </div>
      <div className="catalog-caption">
        <span>Calciatori disponibili</span>
        <span>{result.data?.total ?? '—'}</span>
      </div>
      {result.isPending ? (
        <LoadingState message="Caricamento del listone…" />
      ) : result.isError ? (
        <ErrorState error={result.error} retry={() => void result.refetch()} />
      ) : result.data.items.length === 0 ? (
        <p className="auction-empty">
          Nessun calciatore disponibile con questi filtri.
        </p>
      ) : (
        <div className="catalog-list">
          {result.data.items.map((player) => {
            const eligible =
              canCall &&
              team &&
              player.isAvailable &&
              player.role === currentRole &&
              canBuyRole(team, rules, player.role)
            return (
              <div
                className={`catalog-row ${selectedPlayerId === player.playerId ? 'catalog-row--selected' : ''}`}
                data-role={player.role}
                key={player.playerId}
              >
                <PlayerPhoto url={player.photoUrl} role={player.role} />
                <div>
                  <h3>{player.name}</h3>
                  <p className="player-club-line">
                    <span className={`catalog-role role-${player.role}`}>
                      {player.role}
                    </span>
                    <ClubLabel
                      name={player.clubName}
                      logoUrl={player.clubLogoUrl}
                    />
                  </p>
                  <PlayerValuation player={player} compact />
                </div>
                {canCall ? (
                  <Button
                    variant="outline"
                    disabled={!eligible}
                    onClick={() => onSelect(player)}
                    aria-pressed={selectedPlayerId === player.playerId}
                    aria-label={`Seleziona ${player.name}`}
                  >
                    Scegli <Icon name="plus" />
                  </Button>
                ) : (
                  <span className="catalog-available">Disponibile</span>
                )}
              </div>
            )
          })}
        </div>
      )}
      {result.data && result.data.total > 30 && (
        <nav className="auction-pagination" aria-label="Pagine del listone">
          <Button
            variant="ghost"
            disabled={page <= 1}
            onClick={() => setPage(page - 1)}
          >
            Precedente
          </Button>
          <span>
            {page} / {Math.ceil(result.data.total / 30)}
          </span>
          <Button
            variant="ghost"
            disabled={page * 30 >= result.data.total}
            onClick={() => setPage(page + 1)}
          >
            Successiva
          </Button>
        </nav>
      )}
    </div>
  )
}
