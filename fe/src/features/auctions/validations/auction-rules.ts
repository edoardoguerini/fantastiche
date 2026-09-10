import {
  roles,
  type AuctionTeam,
  type Role,
  type RosterRules,
} from '../types/auction.types'

export function maxOffer(budget: number, emptySlots: number) {
  return emptySlots <= 0 ? 0 : Math.max(0, budget - (emptySlots - 1))
}
export function offerTotal(current: number, increment: number) {
  if (!Number.isSafeInteger(increment) || increment <= 0)
    throw new Error('Incremento non valido')
  return current + increment
}
export function remainingSeconds(
  deadline: string,
  serverTime: string,
  receivedAt: number,
  now: number,
) {
  return Math.max(
    0,
    Math.ceil(
      (Date.parse(deadline) -
        Date.parse(serverTime) -
        Math.max(0, now - receivedAt)) /
        1000,
    ),
  )
}
export function emptySlots(team: AuctionTeam, rules: RosterRules) {
  return roles.reduce(
    (sum, role) => sum + Math.max(0, rules[role.field] - team[role.field]),
    0,
  )
}
export function canBuyRole(team: AuctionTeam, rules: RosterRules, role: Role) {
  const field = roles.find((value) => value.id === role)!.field
  return (
    team[field] < rules[field] &&
    maxOffer(team.budget, emptySlots(team, rules)) >= 1
  )
}

export function keepLatestSession<
  T extends { version: number; serverTime: string },
>(previous: T | undefined, next: T): T {
  if (
    previous &&
    (previous.version > next.version ||
      (previous.version === next.version &&
        Date.parse(previous.serverTime) > Date.parse(next.serverTime)))
  )
    return previous
  return next
}
