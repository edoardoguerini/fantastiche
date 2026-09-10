type MarketValues = {
  currentQuotation?: number | null
  initialQuotation?: number | null
  fvm?: number | null
}

export function PlayerValuation({
  player,
  compact = false,
}: {
  player: MarketValues
  compact?: boolean
}) {
  const { currentQuotation, initialQuotation, fvm } = player
  const difference =
    currentQuotation != null && initialQuotation != null
      ? currentQuotation - initialQuotation
      : null
  if (currentQuotation == null && initialQuotation == null && fvm == null)
    return null
  return (
    <dl
      className={`player-valuation ${compact ? 'player-valuation--compact' : ''}`}
      aria-label="Quotazioni del giocatore"
    >
      {currentQuotation != null && (
        <div>
          <dt>{compact ? 'Qt.' : 'Quotazione'}</dt>
          <dd>{currentQuotation}</dd>
        </div>
      )}
      {!compact && initialQuotation != null && (
        <div>
          <dt>Iniziale</dt>
          <dd>{initialQuotation}</dd>
        </div>
      )}
      {!compact && difference !== null && (
        <div>
          <dt>Variazione</dt>
          <dd>
            {difference > 0 ? '+' : ''}
            {difference}
          </dd>
        </div>
      )}
      {fvm != null && (
        <div>
          <dt>FVM</dt>
          <dd>{fvm}</dd>
        </div>
      )}
    </dl>
  )
}
