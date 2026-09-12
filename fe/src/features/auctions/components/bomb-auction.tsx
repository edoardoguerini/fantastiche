import { useEffect, useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { z } from 'zod'
import { Icon } from '@/components/common/icon'
import { Button } from '@/components/primitives/button'
import { Input } from '@/components/primitives/input'
import type {
  AuctionTeam,
  BombAuctionView,
  RosterRules,
  TimedSession,
} from '../types/auction.types'
import {
  canBuyRole,
  emptySlots,
  maxOffer,
  remainingSeconds,
} from '../validations/auction-rules'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'
import { VictoryCelebration } from './auction-victory'
import { bombLobbyQuotes, lobbyQuoteIndex } from './bomb-lobby-quotes'
import '../auction-bomb.css'

export function isBombActive(bomb: BombAuctionView | null | undefined) {
  return (
    bomb?.status === 'Waiting' ||
    bomb?.status === 'Collecting' ||
    bomb?.status === 'Revealing'
  )
}

export function BombAuction({
  bomb,
  session,
  myTeamId,
  rules,
  connected,
  blocked,
  canManage,
  onBid,
  onCancel,
  onDismiss,
}: {
  bomb: BombAuctionView
  session: TimedSession
  myTeamId: string | null
  rules: RosterRules
  connected: boolean
  blocked: boolean
  canManage: boolean
  onBid: (amount: number) => Promise<void>
  onCancel: () => Promise<void>
  onDismiss: () => void
}) {
  const cancelled = bomb.status === 'Cancelled'
  const [celebrate] = useState(() => isBombActive(bomb))
  const [now, setNow] = useState(() => performance.now())
  const heading = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    heading.current?.focus({ preventScroll: true })
    window.scrollTo?.({ top: 0, behavior: 'instant' })
    const timer = setInterval(() => setNow(performance.now()), 100)
    return () => clearInterval(timer)
  }, [bomb.status])
  const seconds = remainingSeconds(
    bomb.deadline,
    session.serverTime,
    session.receivedAt,
    now,
  )
  const intro = bomb.nextRevealAt
    ? remainingSeconds(
        bomb.nextRevealAt,
        session.serverTime,
        session.receivedAt,
        now,
      )
    : 0
  const team = session.teams.find((value) => value.id === myTeamId)
  const own = bomb.participants.find((value) => value.teamId === myTeamId)
  const ready = bomb.participants.filter((value) => value.hasSubmitted).length
  const terminal = !isBombActive(bomb)
  const collecting = bomb.status === 'Collecting'
  const waiting = bomb.status === 'Waiting'
  const winner = session.teams.find((value) => value.id === bomb.winningTeamId)
  const teamName = (id: string) =>
    session.teams.find((value) => value.id === id)?.name ?? 'Squadra'
  if (bomb.status === 'Completed') {
    return (
      <section
        className="bomb-room bomb-showcase"
        aria-label="Asta Bomba"
        data-phase={bomb.status}
      >
        <VictoryCelebration
          auction={{ ...bomb, currentAmount: bomb.winningAmount ?? 0 }}
          teamName={winner?.name ?? 'Squadra vincitrice'}
          mine={bomb.winningTeamId === myTeamId}
          presentation="page"
          celebrate={celebrate}
          onDismiss={onDismiss}
        />
      </section>
    )
  }
  if (bomb.status === 'Revealing') {
    const offer = bomb.revealedOffers.at(-1)
    return (
      <section
        className="bomb-room bomb-showcase"
        aria-label="Asta Bomba"
        data-phase={bomb.status}
      >
        <h2 className="sr-only" ref={heading} tabIndex={-1}>
          Rivelazione delle offerte
        </h2>
        {canManage && (
          <Button
            className="bomb-showcase-cancel"
            variant="ghost"
            disabled={!connected || blocked}
            onClick={() => void onCancel()}
          >
            Annulla Bomba
          </Button>
        )}
        {!connected && (
          <p className="bomb-connection" role="status">
            Riconnessione in corso. Recuperiamo le offerte e la fase attuale.
          </p>
        )}
        <div
          className="bomb-stage"
          role="status"
          aria-live="polite"
          aria-atomic="true"
        >
          {offer ? (
            <BombRevealOffer
              key={`${bomb.id}:${bomb.round}`}
              offer={offer}
              teams={session.teams}
            />
          ) : (
            <div className="bomb-reveal-intro">
              <p>Offerte chiuse. Ci siamo…</p>
              {intro > 0 ? (
                <strong className="bomb-countdown" aria-hidden="true">
                  {intro}
                </strong>
              ) : (
                <p>Scopriamo le carte.</p>
              )}
            </div>
          )}
        </div>
      </section>
    )
  }
  return (
    <section
      className="bomb-room"
      aria-label="Asta Bomba"
      data-phase={bomb.status}
    >
      {!cancelled && (
        <header className="bomb-header">
          <span className="bomb-mark">
            <Icon name="bomb" variant="jelly" />
          </span>
          <div>
            <h2 ref={heading} tabIndex={-1}>
              {bomb.round > 1
                ? `Spareggio · Round ${bomb.round}`
                : 'È scoppiata la Bomba'}
            </h2>
            <p>{teamName(bomb.callerTeamId)} ha acceso la sfida.</p>
          </div>
          {canManage && !terminal && (
            <Button
              variant="ghost"
              disabled={!connected || blocked}
              onClick={() => void onCancel()}
            >
              Annulla Bomba
            </Button>
          )}
        </header>
      )}
      {!connected && (
        <p className="bomb-connection" role="status">
          Riconnessione in corso. Recuperiamo le offerte e la fase attuale.
        </p>
      )}
      <div className="bomb-arena">
        <div className="bomb-player">
          <PlayerPhoto url={bomb.photoUrl} role={bomb.role} large />
          <span className={`catalog-role role-${bomb.role}`}>{bomb.role}</span>
          <h3>{bomb.name}</h3>
          <ClubLabel name={bomb.clubName} logoUrl={bomb.clubLogoUrl} />
        </div>
        <div className="bomb-action">
          {waiting ? (
            <BombLobby bombId={bomb.id} seconds={seconds} />
          ) : collecting ? (
            <>
              <div
                className="bomb-clock"
                aria-label={`Tempo residuo: ${seconds} secondi`}
              >
                <strong aria-hidden="true">
                  {seconds}
                  <small>s</small>
                </strong>
                <div>
                  <h3>
                    {bomb.round > 1 ? 'Si decide qui.' : 'Quanto lo vuoi?'}
                  </h3>
                  <p>Un’offerta segreta. Una sola conferma.</p>
                </div>
              </div>
              <progress
                className="bomb-time-progress"
                aria-label="Tempo per confermare"
                max={60}
                value={seconds}
              />
              {own?.hasSubmitted || bomb.ownAmount !== null ? (
                <div className="bomb-confirmed" role="status">
                  <Icon name="check" variant="jelly" />
                  <h3>Offerta confermata</h3>
                  {bomb.ownAmount !== null && (
                    <strong>{bomb.ownAmount} crediti</strong>
                  )}
                  <p>
                    La tua offerta è al sicuro. Ora aspettiamo le altre squadre.
                  </p>
                </div>
              ) : own && team ? (
                <BombOfferForm
                  key={`${bomb.id}:${bomb.round}`}
                  bomb={bomb}
                  team={team}
                  rules={rules}
                  disabled={
                    !connected ||
                    blocked ||
                    seconds === 0 ||
                    session.status !== 'Active'
                  }
                  onBid={onBid}
                />
              ) : (
                <p className="bomb-spectator">
                  {!myTeamId
                    ? 'Segui la sfida: tra poco scopriamo le offerte.'
                    : bomb.round > 1
                      ? 'Lo spareggio continua tra le squadre in parità. Segui la rivelazione.'
                      : 'Non hai un posto o il budget disponibile per questo calciatore. Segui la sfida.'}
                </p>
              )}
              <p className="bomb-fineprint">
                {seconds === 0
                  ? 'Tempo scaduto. Attendiamo la rivelazione dal server.'
                  : 'Hai fino a 60 secondi. Quando tutti hanno confermato, o il tempo scade, partono i 30 secondi prima della rivelazione.'}
              </p>
            </>
          ) : (
            <div className="bomb-result" role="status">
              <h3 ref={heading} tabIndex={-1}>
                {cancelled ? 'Bomba annullata' : 'Nessuna offerta confermata'}
              </h3>
              <p>{bomb.name} resta libero. Nessun credito speso.</p>
              <Button onClick={onDismiss}>Torna alla sala</Button>
            </div>
          )}
        </div>
      </div>
      {(waiting || collecting) && (
        <section
          className="bomb-participants"
          aria-label="Squadre partecipanti"
        >
          <div className="bomb-progress-label">
            <h3>{waiting ? 'Squadre in attesa' : 'Pronti alla rivelazione'}</h3>
            {collecting && (
              <span role="status">
                {ready} di {bomb.participants.length} confermate
              </span>
            )}
          </div>
          <ul>
            {bomb.participants.map((participant) => (
              <li
                key={participant.teamId}
                className={
                  !waiting && participant.hasSubmitted ? 'is-ready' : ''
                }
              >
                <span>
                  {teamName(participant.teamId)}
                  {participant.teamId === myTeamId ? ' · Tu' : ''}
                </span>
                <span>
                  {waiting ? (
                    'In attesa'
                  ) : participant.hasSubmitted ? (
                    <>
                      <Icon name="check" variant="jelly" /> Pronta
                    </>
                  ) : (
                    'Sta scegliendo'
                  )}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </section>
  )
}

// Sostituisce la singola offerta soltanto quando arriva un nuovo dato pubblico dal server.
function BombRevealOffer({
  offer,
  teams,
}: {
  offer: BombAuctionView['revealedOffers'][number]
  teams: AuctionTeam[]
}) {
  const [displayed, setDisplayed] = useState(offer)
  const changing =
    displayed.teamId !== offer.teamId || displayed.amount !== offer.amount
  useEffect(() => {
    if (!changing) return
    const timer = setTimeout(() => setDisplayed(offer), 280)
    return () => clearTimeout(timer)
  }, [changing, offer])
  return (
    <div
      key={`${displayed.teamId}:${displayed.amount}`}
      className={`bomb-reveal-offer${changing ? ' is-leaving' : ''}`}
    >
      <h3>
        {teams.find((team) => team.id === displayed.teamId)?.name ?? 'Squadra'}
      </h3>
      <strong>
        {displayed.amount} {displayed.amount === 1 ? 'credito' : 'crediti'}
      </strong>
    </div>
  )
}

function BombOfferForm({
  bomb,
  team,
  rules,
  disabled,
  onBid,
}: {
  bomb: BombAuctionView
  team: AuctionTeam
  rules: RosterRules
  disabled: boolean
  onBid: (amount: number) => Promise<void>
}) {
  const maximum = maxOffer(team.budget, emptySlots(team, rules))
  const minimum = Math.max(1, bomb.minimumAmount)
  const eligible = canBuyRole(team, rules, bomb.role) && maximum >= minimum
  const schema = z.object({
    amount: z
      .string()
      .refine(
        (value) =>
          Number.isSafeInteger(Number(value)) &&
          Number(value) >= minimum &&
          Number(value) <= maximum,
        `Inserisci un intero da ${minimum} a ${maximum}.`,
      ),
  })
  const form = useForm({
    defaultValues: { amount: '' },
    validators: { onSubmit: schema },
    onSubmit: async ({ value }) => {
      if (!disabled && eligible) await onBid(Number(value.amount))
    },
  })
  return (
    <form
      className="bomb-offer-form"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        void form.handleSubmit()
      }}
    >
      <form.Field name="amount">
        {(field) => (
          <>
            <label htmlFor="bomb-amount">La tua offerta segreta</label>
            <div className="bomb-offer-input">
              <Input
                id="bomb-amount"
                type="number"
                inputMode="numeric"
                min={minimum}
                max={maximum}
                step={1}
                placeholder="0"
                autoComplete="off"
                disabled={disabled || !eligible}
                value={field.state.value}
                aria-describedby="bomb-offer-help"
                onChange={(event) => field.handleChange(event.target.value)}
              />
              <span>crediti</span>
            </div>
            <p id="bomb-offer-help">
              {eligible
                ? `Da ${minimum} a ${maximum} crediti. Dopo la conferma non puoi cambiarla.`
                : `Il minimo è ${minimum} crediti; il tuo massimo disponibile è ${maximum}.`}
            </p>
          </>
        )}
      </form.Field>
      <form.Subscribe
        selector={(state) => [state.values.amount, state.isSubmitting] as const}
      >
        {([amount, busy]) => (
          <Button
            type="submit"
            disabled={
              disabled ||
              !eligible ||
              busy ||
              !schema.safeParse({ amount }).success
            }
          >
            {busy ? 'Conferma in corso…' : 'Conferma offerta'}
          </Button>
        )}
      </form.Subscribe>
    </form>
  )
}

function BombLobby({ bombId, seconds }: { bombId: string; seconds: number }) {
  const quoteIndex = lobbyQuoteIndex(bombId, seconds)
  const quote = bombLobbyQuotes[quoteIndex]!
  return (
    <div className="bomb-lobby">
      <div
        className="bomb-clock"
        aria-label={`La sfida inizia tra ${seconds} secondi`}
      >
        <strong aria-hidden="true">
          {seconds}
          <small>s</small>
        </strong>
        <div>
          <h3>La sfida sta per iniziare</h3>
          <p>Tra poco avrai un minuto per confermare la tua offerta.</p>
        </div>
      </div>
      <progress
        className="bomb-time-progress"
        aria-label="Tempo prima della sfida"
        max={60}
        value={seconds}
      />
      {seconds === 0 && (
        <p className="bomb-fineprint" role="status">
          Ci siamo. Attendiamo l’apertura delle offerte.
        </p>
      )}
      <blockquote className="bomb-lobby-quote" key={quoteIndex}>
        <p>«{quote.text}»</p>
        <footer>
          {quote.source ? (
            <a href={quote.source} target="_blank" rel="noopener noreferrer">
              {quote.author}
            </a>
          ) : (
            quote.author
          )}
        </footer>
      </blockquote>
    </div>
  )
}
