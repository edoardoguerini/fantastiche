import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr'
import { authQueryOptions } from '@/features/auth'
import { apiBaseUrl } from '@/lib/api/client'
import { auctionKeys } from '../actions/auction.queries'
import { sessionSchema, type TimedSession } from '../types/auction.types'
import { keepLatestSession } from '../validations/auction-rules'

type LiveStatus = 'connecting' | 'online' | 'reconnecting'
export function useAuctionLive(userId: string, sessionId: string) {
  const client = useQueryClient()
  const [status, setStatus] = useState<LiveStatus>('connecting')
  const [connectedUsers, setConnectedUsers] = useState<number | null>(null)
  useEffect(() => {
    let disposed = false
    let retryTimer: ReturnType<typeof setTimeout> | undefined
    let syncing: Promise<void> | undefined
    let generation = 0
    let resyncRequested = false
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/Auctions`, { withCredentials: true })
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 3000 })
      .configureLogging(LogLevel.None)
      .build()
    const isCurrent = () =>
      !disposed &&
      client.getQueryData(authQueryOptions().queryKey)?.id === userId
    const sync = (markPending = true): Promise<void> => {
      if (syncing) {
        resyncRequested = true
        return syncing
      }
      const syncGeneration = generation
      syncing = (async () => {
        if (!isCurrent() || connection.state !== HubConnectionState.Connected)
          return
        if (markPending) setStatus('connecting')
        const state = sessionSchema.parse(
          await connection.invoke('WatchSession', sessionId),
        )
        if (
          !isCurrent() ||
          state.id !== sessionId ||
          syncGeneration !== generation ||
          connection.state !== HubConnectionState.Connected
        )
          return
        client.setQueryData<TimedSession>(
          auctionKeys.session(userId, sessionId),
          (previous) =>
            keepLatestSession(previous, {
              ...state,
              receivedAt: performance.now(),
            }),
        )
        await client.invalidateQueries({
          queryKey: auctionKeys.room(userId, state.leagueId),
        })
        if (
          isCurrent() &&
          syncGeneration === generation &&
          connection.state === HubConnectionState.Connected &&
          navigator.onLine &&
          !document.hidden
        )
          setStatus('online')
      })()
        .catch(() => {
          if (isCurrent()) {
            setStatus('reconnecting')
            void client.invalidateQueries({ queryKey: auctionKeys.all(userId) })
          }
        })
        .finally(() => {
          syncing = undefined
          if (
            resyncRequested &&
            isCurrent() &&
            navigator.onLine &&
            !document.hidden &&
            connection.state === HubConnectionState.Connected
          ) {
            resyncRequested = false
            void sync()
          }
        })
      return syncing
    }
    const start = async () => {
      if (disposed) return
      try {
        await connection.start()
        if (disposed) {
          await connection.stop()
          return
        }
        await sync()
      } catch {
        if (!disposed) {
          setStatus('reconnecting')
          retryTimer = setTimeout(() => void start(), 3000)
        }
      }
    }
    connection.on(
      'AuctionChanged',
      (event: { sessionId: string; version: number }) => {
        if (!isCurrent() || event.sessionId !== sessionId) return
        const previous = client.getQueryData<TimedSession>(
          auctionKeys.session(userId, sessionId),
        )
        if (!previous || event.version > previous.version)
          void client.invalidateQueries({
            queryKey: auctionKeys.session(userId, sessionId),
          })
      },
    )
    connection.on(
      'AuctionPresenceChanged',
      (event: { sessionId: string; connectedUsers: number }) => {
        if (
          isCurrent() &&
          event.sessionId === sessionId &&
          connection.state === HubConnectionState.Connected &&
          Number.isSafeInteger(event.connectedUsers) &&
          event.connectedUsers >= 0
        )
          setConnectedUsers(event.connectedUsers)
      },
    )
    connection.onreconnecting(() => {
      generation++
      setConnectedUsers(null)
      if (isCurrent()) setStatus('reconnecting')
    })
    connection.onreconnected(() => void sync())
    connection.onclose(() => {
      generation++
      if (isCurrent()) setConnectedUsers(null)
      if (isCurrent()) {
        setStatus('reconnecting')
        retryTimer = setTimeout(() => void start(), 3000)
      }
    })
    const onOffline = () => {
      generation++
      if (isCurrent()) setStatus('reconnecting')
    }
    const onVisible = () => {
      if (document.hidden) onOffline()
      else if (connection.state === HubConnectionState.Connected) void sync()
    }
    window.addEventListener('offline', onOffline)
    window.addEventListener('online', onVisible)
    document.addEventListener('visibilitychange', onVisible)
    // Riprova anche una sottoscrizione fallita senza aspettare una nuova connessione.
    const safety = setInterval(() => {
      if (!document.hidden && connection.state === HubConnectionState.Connected)
        void sync(false)
    }, 15000)
    void start()
    return () => {
      disposed = true
      clearTimeout(retryTimer)
      clearInterval(safety)
      window.removeEventListener('offline', onOffline)
      window.removeEventListener('online', onVisible)
      document.removeEventListener('visibilitychange', onVisible)
      if (connection.state === HubConnectionState.Connected)
        void connection.invoke('UnwatchSession', sessionId).catch(() => {})
      void connection.stop()
    }
  }, [client, userId, sessionId])
  return { status, connectedUsers: status === 'online' ? connectedUsers : null }
}
