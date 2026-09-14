import type { ReactNode } from 'react'
import { Icon } from '@/components/common/icon'
import { Button } from '@/components/primitives/button'
import { roles, type TimedSession } from '../types/auction.types'
import { PlayerPhoto } from './player-photo'
import { ClubLabel } from './club-label'

export function AuctionStage({
  session,
  seconds,
  myTeamId,
  connected,
  canCall,
  onChoose,
  children,
  bidding,
  showLastPurchase = true,
}: {
  session: TimedSession
  seconds: number
  myTeamId: string | null
  connected: boolean
  canCall: boolean
  onChoose: () => void
  children?: ReactNode
  bidding?: ReactNode
  showLastPurchase?: boolean
}) {
  const auction = session.currentAuction
  const caller = session.teams.find((team) => team.id === session.currentTeamId)
  const winner = session.teams.find(
    (team) => team.id === auction?.winningTeamId,
  )
  const open = auction?.status === 'Open'
  const age = playerAge(auction?.birthDate, session.serverTime)
  return (
    <section
      className={`auction-stage ${open ? 'auction-stage--live' : 'auction-stage--waiting'}`}
      data-role={open ? auction.role : undefined}
      aria-label="Asta corrente"
    >
      <div className="auction-stage-top">
        <span className="auction-eyebrow">
          {open
            ? 'Ora all’asta'
            : session.status === 'Completed'
              ? 'Asta conclusa'
              : session.status === 'Paused'
                ? 'Asta in pausa'
                : 'Prossima chiamata'}
        </span>
        {session.currentRole && session.status !== 'Completed' && (
          <span className="auction-role-phase">
            Fase: {roles.find((role) => role.id === session.currentRole)?.label}
          </span>
        )}
      </div>
      {open && auction ? (
        <>
          <div className="auction-contest">
            <div className="auction-player-heading">
              <PlayerPhoto url={auction.photoUrl} role={auction.role} large />
              <div className="auction-player-details">
                <p className="player-club-line">
                  <span className={`catalog-role role-${auction.role}`}>
                    {auction.role}
                  </span>
                  <ClubLabel
                    name={auction.clubName}
                    logoUrl={auction.clubLogoUrl}
                  />
                </p>
                <h2>{auction.name}</h2>
                <section
                  className={`auction-leader ${winner?.id === myTeamId ? 'auction-leader--mine' : ''}`}
                  aria-label="Squadra in testa"
                  aria-live="polite"
                >
                  <span>Sta vincendo</span>{' '}
                  <strong>{winner?.name ?? '—'}</strong>
                </section>
              </div>
            </div>
            <div
              className="auction-current-offer"
              role="group"
              aria-label="Offerta e tempo rimanente"
            >
              <p className="auction-price">
                {auction.currentAmount}
                <small>
                  {auction.currentAmount === 1 ? 'credito' : 'crediti'}
                </small>
              </p>
              <div className="auction-countdown">
                <span
                  className={`auction-timer ${seconds <= 5 ? 'auction-timer--urgent' : ''}`}
                  role="timer"
                  aria-label={`${seconds} secondi rimasti`}
                >
                  {seconds}
                  <small>s</small>
                </span>
                <span className="auction-countdown-label">rimanenti</span>
              </div>
            </div>
          </div>
          <div className="auction-time-track">
            <div
              style={{
                width: `${Math.min(100, (seconds / auction.durationSeconds) * 100)}%`,
              }}
            />
          </div>
          {bidding ?? (
            <p className="auction-stage-note" aria-live="polite">
              {!connected
                ? 'Riconnessione in corso. I rilanci sono temporaneamente sospesi.'
                : seconds === 0
                  ? 'Attendi la conferma dell’aggiudicazione…'
                  : `Ogni rilancio accettato riavvia i ${auction.durationSeconds} secondi.`}
            </p>
          )}
        </>
      ) : (
        <div className="auction-waiting">
          <div className="auction-waiting-icon">
            <Icon
              name={session.status === 'Completed' ? 'trophy' : 'bolt'}
              variant="jelly"
            />
          </div>
          <div className="auction-waiting-copy">
            <h2>
              {!connected
                ? 'Ci riconnettiamo alla sala.'
                : session.status === 'Completed'
                  ? 'Sessione conclusa.'
                  : session.status === 'Paused'
                    ? 'Una breve pausa.'
                    : caller?.id === myTeamId
                      ? `Tocca a te, ${caller.name}.`
                      : `È il turno di ${caller?.name ?? '—'}.`}
            </h2>
            <p>
              {!connected
                ? 'Attendi la sincronizzazione prima di una nuova chiamata.'
                : session.status === 'Completed'
                  ? 'Tutti gli acquisti sono disponibili in Rose e Storico.'
                  : session.status === 'Paused'
                    ? 'La chiamata riprenderà quando l’organizzatore riavvia la sessione.'
                    : caller?.id === myTeamId
                      ? 'Scegli il prossimo calciatore. La chiamata parte da 1 credito.'
                      : 'In attesa della scelta del prossimo calciatore.'}
            </p>
          </div>
          {caller?.id === myTeamId && session.status === 'Active' && (
            <Button
              className="auction-waiting-action"
              disabled={!canCall}
              onClick={onChoose}
            >
              Scegli dal listone
            </Button>
          )}
          {auction && showLastPurchase && (
            <div className="auction-last-purchase" data-role={auction.role}>
              <PlayerPhoto url={auction.photoUrl} role={auction.role} />
              <div>
                <span>Ultimo acquisto</span>
                <strong>{auction.name}</strong>
                <div className="last-purchase-club">
                  <span
                    className={`catalog-role role-${auction.role}`}
                    title={
                      {
                        P: 'Portiere',
                        D: 'Difensore',
                        C: 'Centrocampista',
                        A: 'Attaccante',
                      }[auction.role]
                    }
                  >
                    {auction.role}
                  </span>
                  <ClubLabel
                    name={auction.clubName}
                    logoUrl={auction.clubLogoUrl}
                  />
                </div>
                {(age !== null ||
                  auction.nationality ||
                  auction.preferredFoot) && (
                  <dl className="last-purchase-facts">
                    {age !== null && (
                      <div>
                        <dt>Età</dt>
                        <dd>{age} anni</dd>
                      </div>
                    )}
                    {auction.nationality && (
                      <div>
                        <dt>Nazionalità</dt>
                        <dd>{auction.nationality}</dd>
                      </div>
                    )}
                    {auction.preferredFoot && (
                      <div>
                        <dt>Piede</dt>
                        <dd>{auction.preferredFoot}</dd>
                      </div>
                    )}
                  </dl>
                )}
              </div>
              <p>
                {winner?.name ?? '—'}{' '}
                <strong>{auction.currentAmount} crediti</strong>
              </p>
            </div>
          )}
        </div>
      )}
      {!open && children}
    </section>
  )
}

function playerAge(birthDate: string | null | undefined, serverTime: string) {
  if (!birthDate) return null
  const birth = new Date(`${birthDate.slice(0, 10)}T00:00:00Z`)
  const today = new Date(serverTime)
  if (!Number.isFinite(birth.getTime()) || !Number.isFinite(today.getTime()))
    return null
  const beforeBirthday =
    today.getUTCMonth() < birth.getUTCMonth() ||
    (today.getUTCMonth() === birth.getUTCMonth() &&
      today.getUTCDate() < birth.getUTCDate())
  const age =
    today.getUTCFullYear() - birth.getUTCFullYear() - Number(beforeBirthday)
  return age >= 0 ? age : null
}
