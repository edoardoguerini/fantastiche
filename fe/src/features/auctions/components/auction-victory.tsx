import { useEffect, useRef, useState } from 'react'
import type { AnimationItem } from 'lottie-web'
import fanfareUrl from '@/assets/audio/auction-winner.mp3'
import type { AuctionSession } from '../types/auction.types'
import { PlayerPhoto } from './player-photo'
import '../auction-victory.css'

type PlayerAuction = NonNullable<AuctionSession['currentAuction']>

export function AuctionVictory({
  session,
  myTeamId,
}: {
  session: AuctionSession
  myTeamId: string | null
}) {
  const auction = session.currentAuction
  const [observed, setObserved] = useState(auction)
  const bomb = session.currentBomb
  const [observedBomb, setObservedBomb] = useState(bomb)
  const [award, setAward] = useState<PlayerAuction | null>(null)
  // Solo la transizione confermata dal server: mai il countdown locale o un acquisto già chiuso all’ingresso.
  if (auction?.id !== observed?.id || auction?.status !== observed?.status) {
    setObserved(auction)
    if (
      auction?.status === 'Closed' &&
      auction.closedAt &&
      ((observed?.id === auction.id && observed.status === 'Open') ||
        (bomb?.status === 'Completed' &&
          bomb.playerAuctionId === auction.id &&
          observedBomb?.id === bomb.id &&
          (observedBomb.status === 'Waiting' ||
            observedBomb.status === 'Collecting' ||
            observedBomb.status === 'Revealing')))
    ) {
      setAward(auction)
    }
  }
  if (bomb?.id !== observedBomb?.id || bomb?.status !== observedBomb?.status) {
    setObservedBomb(bomb)
    if (
      (bomb?.status === 'Waiting' || bomb?.status === 'Collecting') &&
      bomb.id !== observedBomb?.id
    )
      setAward(null)
  }
  if (!award || auction?.id !== award.id || auction.status !== 'Closed')
    return null
  const winner = session.teams.find((team) => team.id === award.winningTeamId)
  return (
    <VictoryCelebration
      key={award.id}
      auction={award}
      teamName={winner?.name ?? 'Squadra vincitrice'}
      mine={award.winningTeamId === myTeamId}
    />
  )
}

function VictoryCelebration({
  auction,
  teamName,
  mine,
}: {
  auction: PlayerAuction
  teamName: string
  mine: boolean
}) {
  const [dismissed, setDismissed] = useState(false)
  const [audioBlocked, setAudioBlocked] = useState(false)
  const animationContainer = useRef<HTMLDivElement>(null)
  const audio = useRef<HTMLAudioElement | null>(null)

  useEffect(() => {
    if (dismissed) return
    const player = new Audio(fanfareUrl)
    audio.current = player
    let disposed = false
    queueMicrotask(() => {
      if (disposed) return
      void player.play().catch(() => {
        if (!disposed) setAudioBlocked(true)
      })
    })
    const timeout = setTimeout(() => setDismissed(true), 4500)
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setDismissed(true)
    }
    window.addEventListener('keydown', onKey)
    return () => {
      disposed = true
      clearTimeout(timeout)
      window.removeEventListener('keydown', onKey)
      player.pause()
      player.currentTime = 0
      audio.current = null
    }
  }, [dismissed])

  useEffect(() => {
    const container = animationContainer.current
    if (
      dismissed ||
      !container ||
      window.matchMedia('(prefers-reduced-motion: reduce)').matches
    )
      return
    let disposed = false
    let animation: AnimationItem | undefined
    void import('./victory-confetti')
      .then(({ loadConfetti }) => {
        if (disposed) return
        animation = loadConfetti(container)
      })
      .catch(() => {
        /* Il riepilogo resta visibile anche se l’animazione non si carica. */
      })
    return () => {
      disposed = true
      animation?.destroy()
    }
  }, [dismissed])

  if (dismissed) return null
  return (
    <div className="auction-victory">
      <div
        className="auction-victory-confetti"
        ref={animationContainer}
        aria-hidden="true"
      />
      <section className="auction-victory-card" aria-label="Aggiudicazione">
        <button
          type="button"
          className="auction-victory-close"
          aria-label="Chiudi celebrazione"
          onClick={() => setDismissed(true)}
        >
          ×
        </button>
        <div role="status" aria-atomic="true">
          <p className="auction-victory-eyebrow">
            {mine ? 'È tuo!' : 'Aggiudicato!'}
          </p>
          <h2>{teamName}</h2>
          <div className="auction-victory-player">
            <PlayerPhoto url={auction.photoUrl} role={auction.role} large />
            <div>
              <h3>{auction.name}</h3>
              <p>{auction.clubName}</p>
            </div>
          </div>
          <p className="auction-victory-price">
            <strong>{auction.currentAmount}</strong>{' '}
            {auction.currentAmount === 1 ? 'credito' : 'crediti'}
          </p>
        </div>
        {audioBlocked && (
          <button
            type="button"
            className="auction-victory-play"
            onClick={() => {
              void audio.current
                ?.play()
                .then(() => setAudioBlocked(false))
                .catch(() => {})
            }}
          >
            Riproduci fanfara
          </button>
        )}
      </section>
    </div>
  )
}
