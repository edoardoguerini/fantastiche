import { PlayerValuation } from './player-valuation'
import { useEffect, useRef, useState } from 'react'
import { useInfiniteQuery } from '@tanstack/react-query'
import { Input } from '@/components/primitives/input'
import { Button } from '@/components/primitives/button'
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from '@/components/primitives/select'
import { Icon } from '@/components/common/icon'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { catalogQueryOptions, loadedPlayers } from '../actions/auction.queries'
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
import { InfiniteScrollMore } from './infinite-scroll-more'
import '../auction-catalog.css'

export function CatalogPanel({
  userId,
  sessionId,
  canCall,
  currentRole,
  team,
  rules,
  onSelect,
  selectedPlayerId,
  expanded = false,
}: {
  userId: string
  sessionId: string
  canCall: boolean
  currentRole: Role | null
  team?: AuctionTeam
  rules: RosterRules
  onSelect: (player: CatalogEntry) => void
  selectedPlayerId?: string
  expanded?: boolean
}) {
  const [search, setSearch] = useState('')
  const [debounced, setDebounced] = useState('')
  const [role, setRole] = useState<string>(currentRole ?? '')
  const listRef = useRef<HTMLDivElement>(null)
  const [sort, setSort] = useState('name')
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebounced(search)
      listRef.current?.scrollTo({ top: 0 })
    }, 250)
    return () => clearTimeout(timer)
  }, [search])
  const result = useInfiniteQuery(
    catalogQueryOptions(userId, sessionId, debounced, role, sort),
  )
  const players = loadedPlayers(result.data?.pages)
  return (
    <div
      className={`auction-catalog ${expanded ? 'auction-catalog--expanded' : ''}`}
    >
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
                listRef.current?.scrollTo({ top: 0 })
              }}
            >
              {item.id || item.label}
            </button>
          ))}
        </div>
        <div className="catalog-sort">
          <Select
            value={sort}
            onValueChange={(value) => {
              setSort(value)
              listRef.current?.scrollTo({ top: 0 })
            }}
          >
            <SelectTrigger aria-label="Ordina il listone">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="name">Ordina: Nome</SelectItem>
              <SelectItem value="fvm">Ordina: FVM ↓</SelectItem>
              <SelectItem value="quotation">Ordina: Quotazione ↓</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="catalog-caption">
        <span>Calciatori disponibili</span>
        <span>{result.data?.pages[0]?.total ?? '—'}</span>
      </div>
      {result.isPending ? (
        <LoadingState message="Caricamento del listone…" />
      ) : result.isError && !result.data ? (
        <ErrorState error={result.error} retry={() => void result.refetch()} />
      ) : players.length === 0 ? (
        <p className="auction-empty">
          Nessun calciatore disponibile con questi filtri.
        </p>
      ) : (
        <div className="catalog-list" ref={listRef}>
          {players.map((player) => {
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
                  {!expanded && <PlayerValuation player={player} compact />}
                </div>
                {expanded && (
                  <PlayerValuation player={player} compact variant="tiles" />
                )}
                <Button
                  variant="outline"
                  disabled={!eligible}
                  onClick={() => onSelect(player)}
                  aria-pressed={selectedPlayerId === player.playerId}
                  aria-label={`Seleziona ${player.name}`}
                >
                  Scegli
                </Button>
              </div>
            )
          })}
          <InfiniteScrollMore
            hasMore={result.hasNextPage}
            fetching={result.isFetching}
            error={result.isError}
            label="Carica altri calciatori"
            onLoad={() => {
              if (result.isFetching) return
              if (result.isRefetchError) void result.refetch()
              else void result.fetchNextPage({ cancelRefetch: false })
            }}
          />
        </div>
      )}
    </div>
  )
}
