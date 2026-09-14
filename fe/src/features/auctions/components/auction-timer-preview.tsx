import { useState } from 'react'
import { Button } from '@/components/primitives/button'
import type { League } from '@/features/leagues'
import type { AuctionRoom, TimedSession } from '../types/auction.types'
import { AuctionStage } from './auction-stage'
import { BidControls } from './bid-controls'
import { AuctionAudioControl } from './auction-audio-control'
import { AuctionVictory } from './auction-victory'

/** Anteprima locale: snapshot e rilanci restano in memoria, senza comandi API. */
export function AuctionTimerPreview({
  userId,
  session,
  room,
  league,
}: {
  userId: string
  session: TimedSession
  room: AuctionRoom
  league: League
}) {
  const [snapshot, setSnapshot] = useState<TimedSession>(() => ({
    ...session,
    status: 'Active',
    currentAuction: session.currentAuction
      ? {
          ...session.currentAuction,
          status: 'Open',
          durationSeconds: 30,
          closedAt: null,
        }
      : null,
  }))
  const team = snapshot.teams.find((value) => value.id === room.myTeamId)
  return (
    <div className="auction-timer-preview">
      <AuctionVictory session={snapshot} myTeamId={room.myTeamId} />
      <div
        className="auction-command-status auction-timer-preview-note"
        role="status"
      >
        <p>
          Anteprima · Timer fermo a 20 secondi. I rilanci qui sono solo
          simulati.
        </p>
        {snapshot.currentAuction && (
          <Button
            variant="outline"
            onClick={() => {
              setSnapshot((current) => {
                if (!current.currentAuction) return current
                const closing = current.currentAuction.status === 'Open'
                return {
                  ...current,
                  version: current.version + 1,
                  currentAuction: {
                    ...current.currentAuction,
                    status: closing ? 'Closed' : 'Open',
                    closedAt: closing ? new Date().toISOString() : null,
                  },
                }
              })
            }}
          >
            {snapshot.currentAuction.status === 'Open'
              ? 'Prova vittoria'
              : 'Ripristina anteprima'}
          </Button>
        )}
        <Button variant="outline" asChild>
          <a href={`/leghe/${league.id}/asta`}>Torna all’asta</a>
        </Button>
      </div>
      {snapshot.currentAuction ? (
        <div className="auction-console">
          <div className="auction-workspace">
            <AuctionStage
              session={snapshot}
              seconds={20}
              myTeamId={room.myTeamId}
              connected
              canCall={false}
              onChoose={() => {}}
              bidding={
                <aside
                  className="auction-inline-bidding"
                  aria-label="La tua partecipazione"
                >
                  <BidControls
                    session={snapshot}
                    team={team}
                    rules={league}
                    connected
                    blocked={false}
                    seconds={20}
                    onBid={(amount) => {
                      if (!team) return
                      setSnapshot((current) => ({
                        ...current,
                        currentAuction: current.currentAuction
                          ? {
                              ...current.currentAuction,
                              currentAmount: amount,
                              winningTeamId: team.id,
                            }
                          : null,
                      }))
                    }}
                  />
                </aside>
              }
            />
          </div>
        </div>
      ) : (
        <p>Nessun giocatore ancora disponibile per l’anteprima.</p>
      )}
      <footer className="auction-bottom-bar">
        <div className="auction-preview-footer">
          <span>Anteprima · Timer fermo a 20 s</span>
          <AuctionAudioControl
            userId={userId}
            sessionId={snapshot.id}
            canManage={room.canManage}
            active={snapshot.currentAuction?.status === 'Open'}
            playbackKey={`${snapshot.currentAuction?.id}:${snapshot.currentAuction?.currentAmount}`}
          />
        </div>
      </footer>
    </div>
  )
}
