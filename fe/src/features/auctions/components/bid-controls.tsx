import { useState } from 'react'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import { Icon } from '@/components/common/icon'
import {
  canBuyRole,
  emptySlots,
  maxOffer,
  offerTotal,
} from '../validations/auction-rules'
import type {
  AuctionTeam,
  RosterRules,
  TimedSession,
} from '../types/auction.types'

export function BidControls({
  session,
  team,
  rules,
  connected,
  blocked,
  seconds,
  onBid,
}: {
  session: TimedSession
  team?: AuctionTeam
  rules: RosterRules
  connected: boolean
  blocked: boolean
  seconds: number
  onBid: (amount: number) => void
}) {
  const [custom, setCustom] = useState('')
  const auction = session.currentAuction
  if (!auction || auction.status !== 'Open') return null
  const max = team ? maxOffer(team.budget, emptySlots(team, rules)) : 0
  const eligible = team && canBuyRole(team, rules, auction.role)
  const ownLead = auction.winningTeamId === team?.id
  const disabled =
    !connected ||
    blocked ||
    !eligible ||
    seconds === 0 ||
    ownLead ||
    session.status !== 'Active'
  const amount = Number(custom)
  const customValid =
    Number.isSafeInteger(amount) &&
    amount > auction.currentAmount &&
    amount <= max
  const reason = !connected
    ? 'Riconnessione in corso…'
    : blocked
      ? 'Verifica dell’operazione in corso…'
      : !team
        ? 'Stai seguendo l’asta come spettatore.'
        : ownLead
          ? null
          : !eligible
            ? 'Non hai posti disponibili per questo ruolo.'
            : seconds === 0
              ? 'In attesa dell’esito…'
              : max <= auction.currentAmount
                ? 'Il prezzo supera la tua offerta massima.'
                : `Puoi offrire fino a ${max} crediti.`
  return (
    <section className="auction-bid-controls" aria-label="Rilancia">
      <div className="bid-mobile-summary">
        <span>{auction.name}</span>
        <strong>{auction.currentAmount} cr</strong>
        <span className={seconds <= 5 ? 'bid-time-urgent' : ''}>
          <Icon name="stopwatch" variant="jelly" /> {seconds}s
        </span>
      </div>
      {reason && (
        <p className="bid-context">
          <Icon name="money-bill" variant="jelly" />
          <span>{reason}</span>
        </p>
      )}
      <div className="bid-quick">
        {auction.increments.map((increment) => {
          const total = offerTotal(auction.currentAmount, increment)
          return (
            <Button
              variant="outline"
              key={increment}
              disabled={disabled || total > max}
              onClick={() => onBid(total)}
              aria-label={`Offri ${total} crediti, più ${increment}`}
            >
              <span>+{increment}</span>
              <small>{total} crediti</small>
            </Button>
          )
        })}
      </div>
      <form
        className="bid-custom"
        onSubmit={(event) => {
          event.preventDefault()
          if (!disabled && customValid) {
            onBid(amount)
            setCustom('')
          }
        }}
      >
        <label htmlFor="custom-bid" className="sr-only">
          Offerta totale
        </label>
        <Input
          id="custom-bid"
          type="number"
          inputMode="numeric"
          placeholder="Offerta totale"
          min={auction.currentAmount + 1}
          max={max}
          step={1}
          value={custom}
          onChange={(event) => setCustom(event.target.value)}
          disabled={disabled}
        />
        <Button
          variant="outline"
          type="submit"
          disabled={disabled || !customValid}
        >
          Offri{customValid ? ` ${amount}` : ''}
        </Button>
      </form>
    </section>
  )
}
