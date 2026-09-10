import { Icon } from '@/components/common/icon'
import {
  roles,
  type AuctionTeam,
  type RosterEntry,
  type RosterRules,
} from '../types/auction.types'
import { emptySlots, maxOffer } from '../validations/auction-rules'

export function TeamBoard({
  teams,
  myTeamId,
  currentTeamId,
  rules,
  purchases,
  onSelect,
}: {
  teams: AuctionTeam[]
  myTeamId: string | null
  currentTeamId: string | null
  rules: RosterRules
  purchases: RosterEntry[]
  onSelect: (teamId: string) => void
}) {
  return (
    <section
      className="auction-board"
      aria-label="Tabellone delle squadre"
      tabIndex={0}
    >
      {teams.map((team, index) => {
        const mine = team.id === myTeamId
        const current = team.id === currentTeamId
        const recent = purchases
          .filter((item) => item.teamId === team.id)
          .slice(0, 3)
        const count = roles.reduce((sum, role) => sum + team[role.field], 0)
        return (
          <button
            key={team.id}
            type="button"
            className={`team-column ${mine ? 'team-column--mine' : ''}`}
            onClick={() => onSelect(team.id)}
            aria-label={`Apri rosa ${team.name}`}
          >
            <div className="team-column-top">
              <span
                className={`team-turn ${current ? 'team-turn--active' : ''}`}
              >
                {current ? 'DI TURNO' : `${String(index + 1).padStart(2, '0')}`}
              </span>
              {mine && <span className="team-mine">TU</span>}
            </div>
            <h2>{team.name}</h2>
            <p className="team-budget">
              <Icon name="coins" />
              <strong>{team.budget}</strong>
              <span>crediti</span>
            </p>
            <p className="team-max">
              Offerta max{' '}
              <strong>{maxOffer(team.budget, emptySlots(team, rules))}</strong>
            </p>
            <div className="team-roles">
              {roles.map((role) => (
                <span key={role.id}>
                  <b className={`role-letter role-${role.id}`}>{role.id}</b>
                  {team[role.field]}
                  <small>/{rules[role.field]}</small>
                </span>
              ))}
            </div>
            <div className="team-recent">
              {recent.length ? (
                recent.map((item) => (
                  <p key={item.playerId}>
                    <span className={`role-letter role-${item.role}`}>
                      {item.role}
                    </span>
                    <span>{item.name}</span>
                    <strong>{item.price}</strong>
                  </p>
                ))
              ) : (
                <span className="team-empty">
                  {count
                    ? `${count} giocatori in rosa`
                    : 'In attesa del primo acquisto'}
                </span>
              )}
            </div>
          </button>
        )
      })}
    </section>
  )
}
