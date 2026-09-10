import { Icon } from '@/components/common/icon'
import type { TimedSession } from '../types/auction.types'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'

export function AuctionStage({
  session,
  seconds,
  myTeamId,
}: {
  session: TimedSession
  seconds: number
  myTeamId: string | null
}) {
  const auction = session.currentAuction
  const caller = session.teams.find((team) => team.id === session.currentTeamId)
  const winner = session.teams.find(
    (team) => team.id === auction?.winningTeamId,
  )
  const open = auction?.status === 'Open'
  return (
    <section
      className={`auction-stage ${open ? 'auction-stage--live' : ''}`}
      aria-label="Asta corrente"
    >
      <div className="auction-stage-top">
        <span className="auction-eyebrow">
          {open
            ? 'ORA ALL’ASTA'
            : session.status === 'Completed'
              ? 'ASTA CONCLUSA'
              : session.status === 'Paused'
                ? 'ASTA IN PAUSA'
                : 'PROSSIMA CHIAMATA'}
        </span>
        {open && (
          <span
            className={`auction-timer ${seconds <= 5 ? 'auction-timer--urgent' : ''}`}
            role="timer"
            aria-label={`${seconds} secondi rimasti`}
          >
            <Icon name="stopwatch" />
            {seconds}
            <small>s</small>
          </span>
        )}
      </div>
      {auction ? (
        <>
          <div className="auction-player-heading">
            <PlayerPhoto url={auction.photoUrl} role={auction.role} large />
            <div>
              <p className="player-club-line">
                <span>{auction.role}</span>
                <ClubLabel
                  name={auction.clubName}
                  logoUrl={auction.clubLogoUrl}
                />
              </p>
              <h2>{auction.name}</h2>
            </div>
          </div>
          <div className="auction-price-row">
            <div>
              <span>{open ? 'Offerta attuale' : 'Aggiudicato a'}</span>
              <p className="auction-price">
                {auction.currentAmount}
                <small>crediti</small>
              </p>
            </div>
            <div
              className={`auction-winner ${winner?.id === myTeamId ? 'auction-winner--mine' : ''}`}
            >
              <Icon name={open ? 'flag' : 'check'} />
              <span>
                {winner?.id === myTeamId
                  ? open
                    ? 'Sei in testa'
                    : 'È tuo!'
                  : (winner?.name ?? '—')}
              </span>
            </div>
          </div>
          {open ? (
            <div className="auction-time-track">
              <div
                style={{
                  width: `${Math.min(100, (seconds / auction.durationSeconds) * 100)}%`,
                }}
              />
            </div>
          ) : null}
          <p className="auction-stage-note" aria-live="polite">
            {open
              ? seconds === 0
                ? 'Attendi la conferma dell’aggiudicazione…'
                : `Ogni rilancio accettato riavvia i ${auction.durationSeconds} secondi.`
              : session.status === 'Completed'
                ? 'La sessione è terminata. Puoi consultare rose e acquisti.'
                : session.status === 'Paused'
                  ? 'L’organizzatore riprenderà la sessione.'
                  : caller?.id === myTeamId
                    ? 'Tocca a te. Scegli il prossimo giocatore dal listone.'
                    : `La prossima chiamata spetta a ${caller?.name ?? '—'}.`}
          </p>
        </>
      ) : (
        <div className="auction-waiting">
          <Icon name="futbol" />
          <h2>
            {session.status === 'Paused'
              ? 'Una breve pausa.'
              : caller?.id === myTeamId
                ? 'Tocca a te.'
                : `Tocca a ${caller?.name ?? '—'}.`}
          </h2>
          <p>
            {session.status === 'Paused'
              ? 'La chiamata riprenderà quando l’organizzatore riavvia la sessione.'
              : caller?.id === myTeamId
                ? 'Scegli un giocatore dal listone. La prima offerta è di 1 credito.'
                : 'Il prossimo giocatore apparirà qui appena verrà chiamato.'}
          </p>
        </div>
      )}
    </section>
  )
}
