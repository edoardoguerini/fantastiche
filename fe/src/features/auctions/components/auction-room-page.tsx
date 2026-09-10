import { useEffect, useRef, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { leagueQueryOptions, type League } from '@/features/leagues'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import {
  roomQueryOptions,
  sessionQueryOptions,
  rosterQueryOptions,
  bidsQueryOptions,
} from '../actions/auction.queries'
import { useAuctionLive } from '../hooks/use-auction-live'
import { useAuctionCommand } from '../hooks/use-auction-command'
import { useAuctionClock } from '../hooks/use-auction-clock'
import type { AuctionRoom, TimedSession } from '../types/auction.types'
import { TeamBoard } from './team-board'
import { AuctionStage } from './auction-stage'
import { BidControls } from './bid-controls'
import { CatalogPanel } from './catalog-panel'
import { RosterPanel } from './roster-panel'
import { SessionSetup } from './session-setup'
import { OrganizerControls } from './organizer-controls'
import '../auction.css'

export function AuctionRoomPage({ leagueId }: { leagueId: string }) {
  const { data: user } = useQuery(authQueryOptions())
  const league = useQuery({
    ...leagueQueryOptions(user?.id ?? '', leagueId),
    enabled: !!user,
  })
  if (league.isPending || !user) return <LoadingState />
  if (league.isError)
    return (
      <ErrorState error={league.error} retry={() => void league.refetch()} />
    )
  return (
    <Room
      key={`${user.id}:${leagueId}`}
      userId={user.id}
      league={league.data}
    />
  )
}
function Room({ userId, league }: { userId: string; league: League }) {
  const room = useQuery(
    roomQueryOptions(userId, league.id, league.leagueSeasonId),
  )
  return (
    <div className="auction-room">
      <header className="auction-room-heading">
        <div>
          <Link
            className="auction-back"
            to="/leghe/$leagueId"
            params={{ leagueId: league.id }}
          >
            <Icon name="arrow-left" />
            {league.name}
          </Link>
          <h1>
            Sala d’asta<span>{league.seasonName}</span>
          </h1>
        </div>
        <span className="auction-format">
          CLASSIC · {league.budget} CREDITI
        </span>
      </header>
      {room.isPending ? (
        <LoadingState />
      ) : room.isError ? (
        <ErrorState error={room.error} retry={() => void room.refetch()} />
      ) : room.data.sessionId ? (
        <LiveRoom
          key={room.data.sessionId}
          userId={userId}
          league={league}
          room={room.data}
          sessionId={room.data.sessionId}
        />
      ) : (
        <SessionSetup
          key={room.data.teams.map((team) => team.id).join(',')}
          userId={userId}
          room={room.data}
        />
      )}
    </div>
  )
}
function LiveRoom({
  userId,
  league,
  room,
  sessionId,
}: {
  userId: string
  league: League
  room: AuctionRoom
  sessionId: string
}) {
  const state = useQuery(sessionQueryOptions(userId, sessionId))
  const live = useAuctionLive(userId, sessionId)
  const connected = live === 'online' && !state.isError
  const command = useAuctionCommand(userId, sessionId, connected)
  if (state.isError)
    return <ErrorState error={state.error} retry={() => void state.refetch()} />
  if (!state.data) return <LoadingState message="Ingresso nella sala…" />
  return (
    <SessionView
      userId={userId}
      room={room}
      league={league}
      session={state.data}
      connected={connected}
      command={command}
    />
  )
}
function SessionView({
  userId,
  room,
  league,
  session,
  connected,
  command,
}: {
  userId: string
  room: AuctionRoom
  league: League
  session: TimedSession
  connected: boolean
  command: ReturnType<typeof useAuctionCommand>
}) {
  const client = useQueryClient()
  const seconds = useAuctionClock(session)
  const [tab, setTab] = useState<'catalog' | 'roster' | 'history'>('catalog')
  const [rosterTeam, setRosterTeam] = useState(
    room.myTeamId ?? session.teams[0]?.id ?? '',
  )
  const [newSession, setNewSession] = useState(false)
  const tabsRef = useRef<HTMLDivElement>(null)
  const purchases = useQuery(rosterQueryOptions(userId, session.id))
  const bids = useQuery(
    bidsQueryOptions(userId, session.id, session.currentAuction?.id ?? ''),
  )
  const team = session.teams.find((value) => value.id === room.myTeamId)
  const open = session.currentAuction?.status === 'Open'
  const canCall =
    connected &&
    !command.pending &&
    !command.busy &&
    session.status === 'Active' &&
    !open &&
    !!team &&
    session.currentTeamId === team.id
  useEffect(() => {
    void client.invalidateQueries({
      queryKey: ['auctions', userId, 'catalog', session.id],
    })
    void client.invalidateQueries({
      queryKey: ['auctions', userId, 'roster', session.id],
    })
    void client.invalidateQueries({
      queryKey: ['auctions', userId, 'bids', session.id],
    })
  }, [client, userId, session.id, session.version])
  const selectTeam = (id: string) => {
    setRosterTeam(id)
    setTab('roster')
    tabsRef.current?.scrollIntoView({ behavior: 'auto', block: 'start' })
  }
  return (
    <>
      <div className="auction-status-row">
        <span
          className={`connection-status ${connected ? 'connection-status--online' : ''}`}
          role="status"
        >
          <span />
          {connected ? 'Connesso alla sala' : 'Sincronizzazione in corso…'}
        </span>
        <span>
          {session.status === 'Completed'
            ? 'Sessione conclusa'
            : session.status === 'Paused'
              ? 'Sessione in pausa'
              : 'Sessione aperta'}
        </span>
      </div>
      <TeamBoard
        teams={session.teams}
        myTeamId={room.myTeamId}
        currentTeamId={session.currentTeamId}
        rules={league}
        purchases={purchases.data?.items ?? []}
        onSelect={selectTeam}
      />
      <div className="auction-workspace">
        <div className="auction-main">
          <AuctionStage
            session={session}
            seconds={seconds}
            myTeamId={room.myTeamId}
          />
          <div className="auction-tabs-area" ref={tabsRef}>
            <div
              className="auction-tabs"
              role="tablist"
              aria-label="Dati dell’asta"
            >
              {[
                { id: 'catalog', label: 'Listone', icon: 'list' },
                { id: 'roster', label: 'La mia rosa', icon: 'shirt' },
                { id: 'history', label: 'Storico', icon: 'clock-rotate-left' },
              ].map((item, index) => (
                <button
                  key={item.id}
                  id={`tab-${item.id}`}
                  role="tab"
                  type="button"
                  aria-selected={tab === item.id}
                  tabIndex={tab === item.id ? 0 : -1}
                  aria-controls={`panel-${item.id}`}
                  onKeyDown={(event) => {
                    const ids = ['catalog', 'roster', 'history'] as const
                    const next =
                      event.key === 'ArrowRight'
                        ? (index + 1) % 3
                        : event.key === 'ArrowLeft'
                          ? (index + 2) % 3
                          : event.key === 'Home'
                            ? 0
                            : event.key === 'End'
                              ? 2
                              : null
                    if (next !== null) {
                      event.preventDefault()
                      setTab(ids[next]!)
                      document.getElementById(`tab-${ids[next]}`)?.focus()
                    }
                  }}
                  onClick={() => {
                    setTab(item.id as typeof tab)
                    if (item.id === 'roster')
                      setRosterTeam(room.myTeamId ?? session.teams[0]?.id ?? '')
                  }}
                >
                  {item.label}
                </button>
              ))}
            </div>
            <div
              role="tabpanel"
              id={`panel-${tab}`}
              aria-labelledby={`tab-${tab}`}
              tabIndex={0}
            >
              {tab === 'catalog' ? (
                <CatalogPanel
                  userId={userId}
                  sessionId={session.id}
                  canCall={canCall}
                  team={team}
                  rules={league}
                  onStart={async (player, duration, increments) => {
                    await command.send('Players', {
                      playerId: player.playerId,
                      durationSeconds: duration,
                      increments,
                    })
                  }}
                />
              ) : (
                <RosterPanel
                  key={`${tab}:${rosterTeam}`}
                  userId={userId}
                  sessionId={session.id}
                  teamId={rosterTeam}
                  teams={session.teams}
                  history={tab === 'history'}
                  onTeamChange={setRosterTeam}
                />
              )}
            </div>
          </div>
        </div>
        <aside
          className={`auction-side ${open ? 'auction-side--bidding' : ''}`}
          aria-label="La tua partecipazione"
        >
          <div className="auction-own-team">
            <span className="auction-eyebrow">
              {team ? 'LA TUA SQUADRA' : 'SPETTATORE'}
            </span>
            <h2>{team?.name ?? 'Segui l’asta'}</h2>
            {team && (
              <p>
                <strong>{team.budget}</strong> crediti disponibili
              </p>
            )}
          </div>
          <BidControls
            key={session.currentAuction?.id ?? 'waiting'}
            session={session}
            team={team}
            rules={league}
            connected={connected}
            blocked={!!command.pending || command.busy}
            seconds={seconds}
            onBid={(amount) => {
              if (session.currentAuction)
                void command.send('Bids', {
                  playerAuctionId: session.currentAuction.id,
                  amount,
                })
            }}
          />
          {!open && session.status === 'Active' && (
            <div className="auction-call-notice">
              <Icon name="hand-point-up" />
              <p>
                {canCall
                  ? 'È il tuo turno di chiamata.'
                  : `In attesa di ${session.teams.find((value) => value.id === session.currentTeamId)?.name ?? 'una squadra'}.`}
              </p>
              {canCall && (
                <Button
                  variant="outline"
                  onClick={() => {
                    setTab('catalog')
                    tabsRef.current?.scrollIntoView({
                      behavior: 'auto',
                      block: 'start',
                    })
                    document.getElementById('player-search')?.focus()
                  }}
                >
                  Scegli dal listone
                </Button>
              )}
            </div>
          )}
          {(command.message || command.pending) && (
            <div className="auction-command-status" role="status">
              <p>
                {command.busy ? 'Verifica dell’operazione…' : command.message}
              </p>
              {command.pending && !command.busy && (
                <div>
                  <Button
                    variant="outline"
                    disabled={!connected}
                    onClick={() => void command.recover()}
                  >
                    Verifica esito
                  </Button>
                  <Button
                    variant="ghost"
                    disabled={!connected}
                    onClick={() => void command.retry()}
                  >
                    Reinvia stessa richiesta
                  </Button>
                </div>
              )}
            </div>
          )}
          {session.currentAuction && (
            <div className="auction-recent-bids">
              <h3>Ultimi rilanci</h3>
              {bids.isError ? (
                <p>Storico momentaneamente non disponibile.</p>
              ) : bids.data?.items.length ? (
                [...bids.data.items]
                  .sort((a, b) => b.sequence - a.sequence)
                  .slice(0, 5)
                  .map((bid) => (
                    <p key={bid.id}>
                      <span>
                        {session.teams.find((value) => value.id === bid.teamId)
                          ?.name ?? 'Squadra'}
                      </span>
                      <strong>{bid.amount}</strong>
                    </p>
                  ))
              ) : (
                <p>Le offerte accettate appariranno qui.</p>
              )}
            </div>
          )}
        </aside>
      </div>
      {room.canManage && (
        <OrganizerControls
          session={session}
          disabled={!connected || !!command.pending || command.busy}
          onControl={async (action, teamOrder) => {
            await command.send('Control', { action, teamOrder })
          }}
        />
      )}
      {room.canManage && session.status === 'Completed' && (
        <div className="auction-new-session">
          <Button variant="outline" onClick={() => setNewSession(!newSession)}>
            {newSession ? 'Chiudi preparazione' : 'Prepara una nuova sessione'}
          </Button>
          {newSession && <SessionSetup room={room} userId={userId} />}
        </div>
      )}
    </>
  )
}
