type MarketValues = {
  currentQuotation?: number | null
  initialQuotation?: number | null
  fvm?: number | null
}

export function PlayerValuation({
  player,
  compact = false,
  variant = 'inline',
}: {
  player: MarketValues
  compact?: boolean
  variant?: 'inline' | 'tiles'
}) {
  const { currentQuotation, initialQuotation, fvm } = player
  const difference =
    currentQuotation != null && initialQuotation != null
      ? currentQuotation - initialQuotation
      : null
  const tiles = variant === 'tiles'
  if (
    !tiles &&
    currentQuotation == null &&
    initialQuotation == null &&
    fvm == null
  )
    return null
  return (
    <dl
      className={`player-valuation ${compact ? 'player-valuation--compact' : ''} ${tiles ? 'player-valuation--tiles' : ''}`}
      aria-label="Quotazioni del giocatore"
    >
      {(tiles || currentQuotation != null) && (
        <div>
          <dt>{compact ? 'Qt.' : 'Quotazione'}</dt>
          <dd>
            {currentQuotation ?? <span aria-label="Non disponibile">—</span>}
          </dd>
        </div>
      )}
      {!compact && (tiles || initialQuotation != null) && (
        <div>
          <dt>Iniziale</dt>
          <dd>
            {initialQuotation ?? <span aria-label="Non disponibile">—</span>}
          </dd>
        </div>
      )}
      {!compact && (tiles || difference !== null) && (
        <div>
          <dt>Variazione</dt>
          <dd
            data-trend={
              difference == null || difference === 0
                ? undefined
                : difference > 0
                  ? 'up'
                  : 'down'
            }
          >
            {difference == null ? (
              <span aria-label="Non disponibile">—</span>
            ) : (
              `${difference > 0 ? '+' : ''}${difference}`
            )}
          </dd>
        </div>
      )}
      {(tiles || fvm != null) && (
        <div>
          <dt>FVM</dt>
          <dd>{fvm ?? <span aria-label="Non disponibile">—</span>}</dd>
        </div>
      )}
    </dl>
  )
}
