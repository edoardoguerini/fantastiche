import { useCallback, useEffect, useRef, useState } from 'react'
import { Icon } from '@/components/common/icon'
import soundUrl from '@/assets/audio/auction-event.m4a'

type AudioProps = {
  userId: string
  sessionId: string
  canManage: boolean
  active: boolean
  playbackKey: string
}

export function AuctionAudioControl(props: AudioProps) {
  if (!props.canManage) return null
  return (
    <ManagerAudioControl
      key={`${props.userId}:${props.sessionId}`}
      {...props}
    />
  )
}

function ManagerAudioControl({ userId, active, playbackKey }: AudioProps) {
  const preferenceKey = `auction-audio:${userId}`
  const [enabled, setEnabled] = useState(() => {
    try {
      return localStorage.getItem(preferenceKey) !== 'off'
    } catch {
      return true
    }
  })
  const [blocked, setBlocked] = useState(false)
  const audio = useRef<HTMLAudioElement | null>(null)
  const lastPlayback = useRef<string | null>(null)
  const generation = useRef(0)

  const stop = useCallback(() => {
    generation.current++
    lastPlayback.current = null
    if (audio.current) {
      audio.current.pause()
      audio.current.currentTime = 0
    }
  }, [])

  const play = useCallback(
    (key: string) => {
      stop()
      const attempt = generation.current
      const player = audio.current ?? new Audio(soundUrl)
      audio.current = player
      lastPlayback.current = key
      void player
        .play()
        .then(() => {
          if (attempt === generation.current) setBlocked(false)
        })
        .catch(() => {
          if (attempt === generation.current) setBlocked(true)
        })
    },
    [stop],
  )

  useEffect(() => {
    if (!enabled || !active) stop()
    else if (lastPlayback.current !== playbackKey) play(playbackKey)
  }, [enabled, active, playbackKey, play, stop])
  useEffect(() => stop, [stop])

  const audible = enabled && !blocked
  return (
    <button
      type="button"
      className="auction-audio-toggle"
      aria-label={audible ? 'Disattiva audio asta' : 'Attiva audio asta'}
      aria-pressed={audible}
      title={
        blocked
          ? 'Tocca per abilitare o riprovare l’audio'
          : 'Audio riservato al gestore'
      }
      onClick={() => {
        const next = !audible
        setEnabled(next)
        setBlocked(false)
        try {
          localStorage.setItem(preferenceKey, next ? 'on' : 'off')
        } catch {
          /* Preferenza valida per questa scheda. */
        }
        if (next && active) play(playbackKey)
        else if (!next) stop()
      }}
    >
      <Icon name={audible ? 'volume-low' : 'volume-xmark'} variant="jelly" />
      <span>
        {blocked ? 'Attiva audio' : `Audio ${enabled ? 'on' : 'off'}`}
      </span>
    </button>
  )
}
