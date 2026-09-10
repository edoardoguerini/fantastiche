import { useState } from 'react'
import { Button } from '@/components/primitives/button'
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from '@/components/primitives/select'
import { Icon } from '@/components/common/icon'
import {
  roles,
  type AuctionSession,
  type RosterRules,
} from '../types/auction.types'
import { OrganizerTeamOrder } from './organizer-team-order'
import '../auction-management.css'

export function OrganizerControls({
  session,
  disabled,
  onControl,
  myTeamId,
  rules,
}: {
  session: AuctionSession
  disabled: boolean
  onControl: (
    action: string,
    teamOrder?: string[],
    targetTeamId?: string,
  ) => Promise<void>
  myTeamId: string | null
  rules: RosterRules
}) {
  const [confirm, setConfirm] = useState(false)
  const open = session.currentAuction?.status === 'Open'
  const completed = session.status === 'Completed'
  const blocked = disabled || open || completed
  const order = session.teamOrder.flatMap(
    (id) => session.teams.find((team) => team.id === id) ?? [],
  )
  const currentIndex = order.findIndex(
    (team) => team.id === session.currentTeamId,
  )
  const caller = order[currentIndex]
  const capacity =
    rules.goalkeepers + rules.defenders + rules.midfielders + rules.forwards
  const count = (team: (typeof order)[number]) =>
    team.goalkeepers + team.defenders + team.midfielders + team.forwards
  const role = roles.find((value) => value.id === session.currentRole)
  const hasRoleSpace = (team: (typeof order)[number]) =>
    !!role && team[role.field] < rules[role.field]
  const next = Array.from(
    { length: order.length },
    (_, offset) => order[(currentIndex + offset + 1) % order.length],
  ).find((team) => team && hasRoleSpace(team))
  const purchased = order.reduce((sum, team) => sum + count(team), 0)
  const spent = order.reduce((sum, team) => sum + rules.budget - team.budget, 0)
  const status = completed
    ? 'Sessione conclusa'
    : session.status === 'Paused'
      ? 'Sessione in pausa'
      : 'Sessione aperta'
  return (
    <div className="auction-management">
      <header className="management-header">
        <div>
          <h2>Gestisci asta</h2>
          <p>Controlla la sessione e l’ordine delle chiamate.</p>
        </div>
        <span className="management-status" data-status={session.status}>
          <span aria-hidden="true" />
          {status}
        </span>
      </header>
      <div className="management-grid">
        <div className="management-main">
          <section className="management-card" aria-label="Turno corrente">
            <h3 className="management-label">Turno corrente</h3>
            <div className="management-caller">
              <span className="management-avatar" aria-hidden="true">
                {caller?.name
                  .split(/\s+/)
                  .slice(0, 2)
                  .map((word) => word[0])
                  .join('') ?? '—'}
              </span>
              <div>
                <h3>{caller?.name ?? 'Asta conclusa'}</h3>
                <p>
                  {open
                    ? `${session.currentAuction!.name} è all’asta.`
                    : completed
                      ? 'Gli acquisti sono disponibili nelle rose.'
                      : session.status === 'Paused'
                        ? 'Riprendi la sessione per continuare le chiamate.'
                        : 'In attesa della scelta del prossimo calciatore.'}
                </p>
              </div>
            </div>
            <dl className="management-turn-details">
              <div>
                <dt>Ruolo in corso</dt>
                <dd>{!completed ? (role?.label ?? '—') : '—'}</dd>
              </div>
              <div>
                <dt>Posizione</dt>
                <dd>
                  {currentIndex >= 0
                    ? `${currentIndex + 1} / ${order.length}`
                    : '—'}
                </dd>
              </div>
              <div>
                <dt>Prossimo turno</dt>
                <dd>{!completed ? (next?.name ?? '—') : '—'}</dd>
              </div>
            </dl>
            <div className="management-actions">
              <Button
                variant="outline"
                disabled={blocked}
                onClick={() =>
                  void onControl(
                    session.status === 'Paused' ? 'Resume' : 'Pause',
                  )
                }
              >
                <Icon
                  name={session.status === 'Paused' ? 'play' : 'pause'}
                  variant="jelly"
                />
                {session.status === 'Paused' ? 'Riprendi' : 'Pausa'}
              </Button>
              <Button
                disabled={blocked}
                onClick={() => void onControl('SkipTurn')}
              >
                Salta turno
              </Button>
              <Button
                className="management-end"
                variant="ghost"
                disabled={blocked}
                onClick={() => setConfirm(true)}
              >
                Termina asta
              </Button>
            </div>
            <div className="management-jump">
              <label htmlFor="management-jump-trigger">Vai al turno di</label>
              <Select
                value=""
                disabled={blocked}
                onValueChange={(targetTeamId) => {
                  if (targetTeamId)
                    void onControl('GoToTurn', undefined, targetTeamId)
                }}
              >
                <SelectTrigger
                  id="management-jump-trigger"
                  aria-label="Vai al turno di"
                >
                  <SelectValue placeholder="Scegli una squadra…" />
                </SelectTrigger>
                <SelectContent>
                  {order
                    .filter((team) => team.id !== caller?.id)
                    .map((team) => (
                      <SelectItem
                        key={team.id}
                        value={team.id}
                        disabled={!hasRoleSpace(team)}
                      >
                        {team.name}
                        {!hasRoleSpace(team) ? ' · Ruolo completato' : ''}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>
            {open && (
              <p className="management-hint">
                Puoi gestire i turni quando termina il calciatore in corso.
              </p>
            )}
            {disabled && (
              <p className="management-hint">
                Controlli in attesa della connessione o della conferma del
                server.
              </p>
            )}
            {confirm && (
              <div className="auction-notice" role="alert">
                <p>
                  Terminare questa sessione? Gli acquisti restano nelle rose.
                </p>
                <div className="organizer-buttons">
                  <Button variant="ghost" onClick={() => setConfirm(false)}>
                    Annulla
                  </Button>
                  <Button
                    disabled={blocked}
                    onClick={() => {
                      void onControl('Complete')
                      setConfirm(false)
                    }}
                  >
                    Conferma conclusione
                  </Button>
                </div>
              </div>
            )}
          </section>
          <OrganizerTeamOrder
            session={session}
            myTeamId={myTeamId}
            rules={rules}
            disabled={blocked}
            onControl={onControl}
          />
        </div>
        <aside className="management-sidebar">
          <section className="management-card" aria-label="Riepilogo rose">
            <h3 className="management-label">Riepilogo rose</h3>
            <dl className="management-summary">
              <div>
                <dt>Partecipanti</dt>
                <dd>{order.length}</dd>
              </div>
              <div>
                <dt>Assegnati</dt>
                <dd>{purchased}</dd>
              </div>
              <div>
                <dt>Crediti spesi</dt>
                <dd>{spent}</dd>
              </div>
              <div>
                <dt>Posti da completare</dt>
                <dd>{Math.max(0, order.length * capacity - purchased)}</dd>
              </div>
            </dl>
            <p className="management-hint">
              Totali delle squadre in questa stagione.
            </p>
          </section>
        </aside>
      </div>
    </div>
  )
}
