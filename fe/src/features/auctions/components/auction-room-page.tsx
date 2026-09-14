import { useEffect, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useRouterState } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { leagueQueryOptions, type League } from '@/features/leagues'
import { ErrorState, LoadingState } from '@/components/common/page-state'
import { Icon } from '@/components/common/icon'
import { Button } from '@/components/primitives/button'
import {
  AppBrandContent,
  AppHeaderContent,
  AppAccountContent,
} from '@/components/layout/app-shell'
import {
  roomQueryOptions,
  sessionQueryOptions,
  bidsQueryOptions,
} from '../actions/auction.queries'
import { useAuctionLive } from '../hooks/use-auction-live'
import { useAuctionCommand } from '../hooks/use-auction-command'
import { useAuctionClock } from '../hooks/use-auction-clock'
import { canBuyRole } from '../validations/auction-rules'
import { useTestCaller } from '../hooks/use-test-caller'
import type {
  AuctionRoom,
  TimedSession,
  CatalogEntry,
} from '../types/auction.types'
import { TeamBoard } from './team-board'
import { AuctionPresence } from './auction-presence'
import {
  AuctionNavigation,
  AuctionTurn,
  type AuctionSection,
} from './auction-navigation'
import { AuctionStage } from './auction-stage'
import { AuctionTimerPreview } from './auction-timer-preview'
import { BombAuction, isBombActive } from './bomb-auction'
import { AuctionVictory } from './auction-victory'
import { BidControls } from './bid-controls'
import { CatalogPanel } from './catalog-panel'
import { PlayerCallForm } from './player-call-form'
import { RosterPanel } from './roster-panel'
import { RecentPurchases } from './recent-purchases'
import { PlayerSearchDialog } from './player-search-dialog'
import { SessionSetup } from './session-setup'
import { OrganizerControls } from './organizer-controls'
import '../auction.css'
import '../auction-navigation.css'
import '../auction-console.css'
import '../auction-rosters.css'
import '../auction-live-layout.css'

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
  const myTeam = room.data?.teams.find(
    (team) => team.id === room.data?.myTeamId,
  )
  return (
    <div className="auction-room">
      <AppBrandContent>
        <span className="auction-header-identity">
          <strong title={league.name}>{league.name}</strong>
          {myTeam && <span title={myTeam.name}>{myTeam.name}</span>}
        </span>
      </AppBrandContent>
      <h1 className="sr-only">Sala d’asta · {league.name}</h1>
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
  const search = useRouterState({
    select: (router) => router.location.searchStr,
  })
  const live = useAuctionLive(userId, sessionId)
  const connected = live.status === 'online' && !state.isError
  const command = useAuctionCommand(userId, sessionId, connected)
  if (state.isError)
    return <ErrorState error={state.error} retry={() => void state.refetch()} />
  if (!state.data) return <LoadingState message="Ingresso nella sala…" />
  if (
    import.meta.env.DEV &&
    new URLSearchParams(search).get('preview') === 'timer'
  )
    return (
      <AuctionTimerPreview
        userId={userId}
        session={state.data}
        room={room}
        league={league}
      />
    )
  return (
    <>
      <AppAccountContent>
        {state.data.teams
          .filter((team) => team.id === room.myTeamId)
          .map((team) => (
            <span
              key={team.id}
              className="auction-account-budget"
              role="status"
              aria-label="Il tuo budget"
            >
              <strong>{team.budget}</strong> crediti
              <span className="auction-budget-qualifier"> disponibili</span>
            </span>
          ))}
      </AppAccountContent>
      <AppHeaderContent>
        <AuctionPresence
          connected={connected}
          connectedUsers={live.connectedUsers}
          users={live.presenceUsers}
          participants={room.participants}
          userId={userId}
        />
      </AppHeaderContent>
      <SessionView
        testCaller={
          import.meta.env.DEV &&
          new URLSearchParams(search).get('testCaller') === 'me'
        }
        userId={userId}
        room={room}
        league={league}
        session={state.data}
        connected={connected}
        command={command}
      />
    </>
  )
}
function SessionView({
  testCaller,
  userId,
  room,
  league,
  session,
  connected,
  command,
}: {
  testCaller: boolean
  userId: string
  room: AuctionRoom
  league: League
  session: TimedSession
  connected: boolean
  command: ReturnType<typeof useAuctionCommand>
}) {
  const client = useQueryClient()
  const seconds = useAuctionClock(session)
  const bomb = session.currentBomb
  const bombActive = isBombActive(bomb)
  const dismissedBombKey = `auction-dismissed-bomb:${userId}:${session.id}`
  const [dismissedBomb, setDismissedBomb] = useState<string | null>(() => {
    try {
      return sessionStorage.getItem(dismissedBombKey)
    } catch {
      return null
    }
  })
  const showBomb = !!bomb && (bombActive || dismissedBomb !== bomb.id)
  const fixedCaller = useTestCaller({
    enabledInitially: testCaller,
    session,
    myTeamId: room.myTeamId,
    canManage: room.canManage,
    blocked:
      !connected ||
      bombActive ||
      !!command.pending ||
      command.busy ||
      !session.currentRole ||
      !session.teams.some(
        (value) =>
          value.id === room.myTeamId &&
          canBuyRole(value, league, session.currentRole!),
      ),
    send: command.send,
  })
  const [tab, setTab] = useState<AuctionSection>('live')
  const [searchDialogVersion, setSearchDialogVersion] = useState<number | null>(
    null,
  )
  const [selection, setSelection] = useState<{
    player: CatalogEntry
    version: number
  } | null>(null)
  const [rosterTeam, setRosterTeam] = useState('')
  const [rosterOpened, setRosterOpened] = useState(false)
  const [newSession, setNewSession] = useState(false)
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
    !bombActive &&
    !!team &&
    session.currentTeamId === team.id &&
    !!session.currentRole &&
    canBuyRole(team, league, session.currentRole)
  const selected =
    selection?.version === session.version &&
    selection.player.role === session.currentRole &&
    !open &&
    !bombActive &&
    session.status === 'Active' &&
    session.currentTeamId === team?.id
      ? selection.player
      : null
  useEffect(() => {
    void client.invalidateQueries({
      queryKey: ['auctions', userId, 'catalog', session.id],
    })
    void client.invalidateQueries({
      queryKey: ['auctions', userId, 'bids', session.id],
    })
  }, [client, userId, session.id, session.version])
  const rosterRevision = session.teams
    .map(({ id, goalkeepers, defenders, midfielders, forwards }) =>
      [id, goalkeepers, defenders, midfielders, forwards].join(':'),
    )
    .join('|')
  useEffect(() => {
    void client.invalidateQueries({
      queryKey: ['auctions', userId, 'roster', session.id],
    })
  }, [client, userId, session.id, rosterRevision])
  const selectSection = (next: AuctionSection) => {
    setRosterTeam('')
    if (next === 'roster') setRosterOpened(true)
    setTab(next)
    window.scrollTo({ top: 0, behavior: 'instant' })
  }
  const selectTeam = (id: string) => {
    selectSection('roster')
    setRosterTeam(id)
  }
  const selectPlayer = (player: CatalogEntry) => {
    if (!canCall || player.role !== session.currentRole) return
    setSearchDialogVersion(null)
    setSelection({ player, version: session.version })
    selectSection('live')
    requestAnimationFrame(() => {
      const timer = document.getElementById('call-duration')
      timer
        ?.closest('form')
        ?.scrollIntoView({ block: 'start', behavior: 'instant' })
      timer?.focus({ preventScroll: true })
    })
  }
  return (
    <div className="auction-session" data-section={tab}>
      <AuctionVictory session={session} myTeamId={room.myTeamId} />
      <AuctionTurn
        session={session}
        myTeamId={room.myTeamId}
        connected={connected}
        visuallyHidden
      />
      {(command.pending || command.message) && (
        <div className="auction-command-status" role="status">
          <p>{command.busy ? 'Verifica dell’operazione…' : command.message}</p>
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
      {showBomb && bomb && (
        <BombAuction
          key={bomb.id}
          bomb={bomb}
          session={session}
          myTeamId={room.myTeamId}
          rules={league}
          connected={connected}
          blocked={!!command.pending || command.busy}
          canManage={room.canManage}
          onBid={async (amount) => {
            await command.send('BombBids', {
              bombAuctionId: bomb.id,
              round: bomb.round,
              amount,
            })
          }}
          onCancel={async () => {
            await command.send('CancelBomb', { bombAuctionId: bomb.id })
          }}
          onDismiss={() => {
            setDismissedBomb(bomb.id)
            try {
              sessionStorage.setItem(dismissedBombKey, bomb.id)
            } catch {
              // Il ritorno alla sala resta disponibile anche senza storage.
            }
            window.scrollTo({ top: 0, behavior: 'instant' })
          }}
        />
      )}
      {searchDialogVersion === session.version && canCall && !showBomb && (
        <PlayerSearchDialog
          userId={userId}
          sessionId={session.id}
          currentRole={session.currentRole}
          team={team}
          rules={league}
          canCall={canCall}
          onSelect={selectPlayer}
          onClose={() => setSearchDialogVersion(null)}
        />
      )}
      <div className="auction-standard-room" hidden={showBomb}>
        <AuctionNavigation
          userId={userId}
          canManage={room.canManage}
          active={tab}
          onSelect={selectSection}
          session={session}
          seconds={seconds}
          connected={connected}
        />
        <div
          className={`auction-console ${tab === 'live' ? 'auction-console--live' : ''}`}
        >
          <section
            id="panel-live"
            role="tabpanel"
            aria-labelledby="tab-live"
            tabIndex={0}
            hidden={tab !== 'live'}
          >
            <div className="auction-live-top">
              <div className="auction-workspace">
                <div className="auction-main">
                  {selected ? (
                    <PlayerCallForm
                      key={selected.playerId}
                      player={selected}
                      disabled={!canCall}
                      bombUsed={
                        !!team && session.usedBombTeamIds?.includes(team.id)
                      }
                      onCancel={() => setSelection(null)}
                      onBomb={async () => {
                        await command.send('Bombs', {
                          playerId: selected.playerId,
                        })
                      }}
                      onStart={async (duration, increments) => {
                        await command.send('Players', {
                          playerId: selected.playerId,
                          durationSeconds: duration,
                          increments,
                        })
                      }}
                    />
                  ) : (
                    <AuctionStage
                      session={session}
                      seconds={seconds}
                      myTeamId={room.myTeamId}
                      connected={connected}
                      canCall={canCall}
                      showLastPurchase={false}
                      onChoose={() => setSearchDialogVersion(session.version)}
                    >
                      {canCall && (
                        <button
                          type="button"
                          className="auction-player-search-trigger"
                          aria-label="Apri ricerca calciatore"
                          aria-haspopup="dialog"
                          onClick={() =>
                            setSearchDialogVersion(session.version)
                          }
                        >
                          <Icon name="magnifying-glass" />
                          Cerca un calciatore…
                        </button>
                      )}
                    </AuctionStage>
                  )}
                </div>
                {open && (
                  <aside
                    id="live-bid-controls"
                    tabIndex={-1}
                    className={`auction-side ${open ? 'auction-side--bidding' : ''}`}
                    aria-label="La tua partecipazione"
                  >
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
                    {open && session.currentAuction && (
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
                                  {session.teams.find(
                                    (value) => value.id === bid.teamId,
                                  )?.name ?? 'Squadra'}
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
                )}
              </div>
              <RecentPurchases
                userId={userId}
                sessionId={session.id}
                teams={session.teams}
                onShowAll={() => selectSection('history')}
              />
            </div>
            <TeamBoard
              userId={userId}
              sessionId={session.id}
              teams={session.teams}
              myTeamId={room.myTeamId}
              currentTeamId={session.currentTeamId}
              rules={league}
              onSelect={selectTeam}
            />
          </section>
          <section
            className="auction-section"
            id="panel-catalog"
            role="tabpanel"
            aria-labelledby="tab-catalog"
            tabIndex={0}
            hidden={tab !== 'catalog'}
          >
            <CatalogPanel
              active={tab === 'catalog'}
              expanded
              key={`${session.id}:${session.currentRole}`}
              currentRole={session.currentRole}
              userId={userId}
              sessionId={session.id}
              canCall={canCall}
              team={team}
              rules={league}
              selectedPlayerId={selected?.playerId}
              onSelect={selectPlayer}
            />
          </section>
        </div>
        <section
          className="auction-section"
          id="panel-roster"
          role="tabpanel"
          aria-labelledby="tab-roster"
          tabIndex={0}
          hidden={tab !== 'roster'}
        >
          {rosterOpened && (
            <TeamBoard
              userId={userId}
              sessionId={session.id}
              teams={session.teams}
              myTeamId={room.myTeamId}
              currentTeamId={session.currentTeamId}
              rules={league}
              onSelect={selectTeam}
              expanded
              active={tab === 'roster'}
              selectedTeamId={rosterTeam}
            />
          )}
        </section>
        <section
          className="auction-section"
          id="panel-history"
          role="tabpanel"
          aria-labelledby="tab-history"
          tabIndex={0}
          hidden={tab !== 'history'}
        >
          <RosterPanel
            userId={userId}
            sessionId={session.id}
            teams={session.teams}
          />
        </section>
        {room.canManage && (
          <section
            className="auction-section auction-management-panel"
            id="panel-manage"
            role="tabpanel"
            aria-labelledby="tab-manage"
            tabIndex={0}
            hidden={tab !== 'manage'}
          >
            <OrganizerControls
              session={session}
              myTeamId={room.myTeamId}
              rules={league}
              disabled={
                !connected || bombActive || !!command.pending || command.busy
              }
              onControl={async (action, teamOrder, targetTeamId) => {
                await command.send('Control', {
                  action,
                  teamOrder,
                  targetTeamId,
                })
              }}
            />
            {import.meta.env.DEV && room.myTeamId && (
              <label className="management-test-caller">
                <input
                  type="checkbox"
                  checked={fixedCaller.enabled}
                  onChange={(event) =>
                    fixedCaller.setEnabled(event.target.checked)
                  }
                />
                Test · Mantieni il turno alla mia squadra
              </label>
            )}
            {room.canManage && session.status === 'Completed' && (
              <div className="auction-new-session">
                <Button
                  variant="outline"
                  onClick={() => setNewSession(!newSession)}
                >
                  {newSession
                    ? 'Chiudi preparazione'
                    : 'Prepara una nuova sessione'}
                </Button>
                {newSession && <SessionSetup room={room} userId={userId} />}
              </div>
            )}
          </section>
        )}
      </div>
    </div>
  )
}
