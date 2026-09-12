import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import type { TimedSession } from '../types/auction.types'
import { AuctionAudioControl } from './auction-audio-control'

const sections = [
  { id: 'live', label: 'Live', icon: 'bolt' },
  { id: 'catalog', label: 'Listone', icon: 'list' },
  { id: 'roster', label: 'Rose', icon: 'shirt' },
  { id: 'history', label: 'Storico', icon: 'clock' },
  { id: 'manage', label: 'Gestisci asta', icon: 'sliders' },
] as const

export type AuctionSection = (typeof sections)[number]['id']

export function AuctionNavigation({
  userId,
  active,
  onSelect,
  session,
  seconds,
  connected,
  canManage,
  myTeamId,
  initialBudget,
}: {
  userId: string
  active: AuctionSection
  onSelect: (section: AuctionSection) => void
  session: TimedSession
  seconds: number
  connected: boolean
  canManage: boolean
  myTeamId: string | null
  initialBudget: number
}) {
  const team = session.teams.find((item) => item.id === myTeamId)
  const auction = session.currentAuction
  const visibleSections = sections.filter(
    (section) => section.id !== 'manage' || canManage,
  )
  return (
    <footer className="auction-bottom-bar">
      {active !== 'live' && auction?.status === 'Open' && (
        <section
          className="auction-mini-live"
          aria-label="Riepilogo asta in corso"
        >
          <div className="auction-mini-player">
            <span>
              {connected ? 'Asta in corso' : 'Riconnessione in corso…'}
            </span>
            <strong>{auction.name}</strong>
          </div>
          <div className="auction-mini-price">
            <strong>{auction.currentAmount} crediti</strong>
            <span className={seconds <= 5 ? 'bid-time-urgent' : ''}>
              {connected ? (
                seconds === 0 ? (
                  'In attesa dell’esito…'
                ) : (
                  <>
                    <Icon name="stopwatch" variant="jelly" /> {seconds} s
                  </>
                )
              ) : (
                'Aggiornamento in attesa'
              )}
            </span>
          </div>
          <Button
            variant="outline"
            onClick={() => {
              onSelect('live')
              requestAnimationFrame(() =>
                document.getElementById('live-bid-controls')?.focus(),
              )
            }}
            aria-label="Torna ai rilanci"
          >
            <span className="auction-mini-action-label">Vai al Live</span>{' '}
            <Icon name="arrow-up" variant="jelly" />
          </Button>
        </section>
      )}
      {team && (
        <section className="auction-budget-strip" aria-label="Il tuo budget">
          <div className="auction-budget-content">
            <p className="auction-budget-amount">
              <strong>{team.budget}</strong> <span>crediti disponibili</span>
            </p>
            <progress
              aria-label={`Budget residuo di ${team.name}`}
              aria-valuetext={`${team.budget} crediti disponibili su ${initialBudget} iniziali`}
              value={Math.max(0, Math.min(team.budget, initialBudget))}
              max={Math.max(1, initialBudget)}
            />
          </div>
        </section>
      )}
      <div className="auction-bottom-navigation" data-manage={canManage}>
        <div
          className="auction-bottom-tabs"
          data-manage={canManage}
          role="tablist"
          aria-label="Sezioni della sala d’asta"
        >
          {visibleSections.map((section, index) => (
            <button
              key={section.id}
              type="button"
              role="tab"
              id={`tab-${section.id}`}
              aria-selected={active === section.id}
              aria-controls={`panel-${section.id}`}
              aria-label={section.label}
              tabIndex={active === section.id ? 0 : -1}
              onClick={() => onSelect(section.id)}
              onKeyDown={(event) => {
                const next =
                  event.key === 'ArrowRight'
                    ? (index + 1) % visibleSections.length
                    : event.key === 'ArrowLeft'
                      ? (index + visibleSections.length - 1) %
                        visibleSections.length
                      : event.key === 'Home'
                        ? 0
                        : event.key === 'End'
                          ? visibleSections.length - 1
                          : null
                if (next !== null) {
                  event.preventDefault()
                  const target = visibleSections[next]!
                  onSelect(target.id)
                  document
                    .getElementById(`tab-${target.id}`)
                    ?.focus({ preventScroll: true })
                }
              }}
            >
              <Icon name={section.icon} variant="jelly" />
              <span>{section.label}</span>
            </button>
          ))}
        </div>
        <AuctionAudioControl
          userId={userId}
          sessionId={session.id}
          canManage={canManage}
          active={
            connected &&
            session.status === 'Active' &&
            auction?.status === 'Open' &&
            seconds > 0
          }
          playbackKey={`${auction?.id}:${auction?.currentAmount}:${auction?.deadline}`}
        />
      </div>
    </footer>
  )
}

export function AuctionTurn({
  session,
  myTeamId,
  connected,
  visuallyHidden = false,
}: {
  session: TimedSession
  myTeamId: string | null
  connected: boolean
  visuallyHidden?: boolean
}) {
  const current = session.teams.find(
    (team) => team.id === session.currentTeamId,
  )
  const title = !connected
    ? 'Sincronizzazione in corso'
    : session.status === 'Completed'
      ? 'Asta conclusa'
      : session.status === 'Paused'
        ? 'Asta in pausa'
        : session.currentAuction?.status === 'Open'
          ? `Ha chiamato ${current?.name ?? '—'}`
          : current?.id === myTeamId
            ? 'Tocca a te'
            : `Chiama: ${current?.name ?? '—'}`
  return (
    <div
      className={visuallyHidden ? 'sr-only' : 'auction-turn-inline'}
      role="status"
      aria-label="Turno di chiamata"
    >
      <Icon name="bolt" variant="jelly" />
      <span>{title}</span>
    </div>
  )
}
