import { useCallback, useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authQueryOptions } from '@/features/auth'
import { ApiError, errorMessage } from '@/lib/api/error'
import {
  pendingCommandSchema,
  readPending,
  storePending,
  sendAuctionCommand,
  recoverAuctionCommand,
  type PendingCommand,
  type CommandKind,
} from '../actions/auction.commands'
import { auctionKeys } from '../actions/auction.queries'
import type { AuctionReceipt } from '../types/auction.types'

export function useAuctionCommand(
  userId: string,
  sessionId: string,
  connected: boolean,
) {
  const client = useQueryClient()
  const [pending, setPending] = useState(() => readPending(userId, sessionId))
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const lock = useRef(false)
  const abort = useRef<AbortController | null>(null)
  const alive = useRef(true)
  useEffect(() => {
    alive.current = true
    return () => {
      alive.current = false
      abort.current?.abort()
    }
  }, [])
  const refresh = useCallback(
    () =>
      client.invalidateQueries({
        queryKey: auctionKeys.all(userId),
        // I conteggi dello snapshot aggiornano le rose dopo le aggiudicazioni.
        predicate: (query) => query.queryKey[2] !== 'roster',
      }),
    [client, userId],
  )
  const finish = useCallback(
    async (receipt: AuctionReceipt) => {
      if (
        !alive.current ||
        client.getQueryData(authQueryOptions().queryKey)?.id !== userId
      )
        return
      storePending(userId, sessionId, null)
      setPending(null)
      setMessage(
        receipt.accepted
          ? 'Operazione confermata.'
          : (receipt.message ??
              'Operazione non accettata. Controlla lo stato aggiornato.'),
      )
      await refresh()
    },
    [client, refresh, sessionId, userId],
  )
  const run = useCallback(
    async (command: PendingCommand, mode: 'initial' | 'recover' | 'retry') => {
      if (lock.current || !connected) return
      lock.current = true
      setBusy(true)
      abort.current = new AbortController()
      try {
        const receipt =
          mode === 'recover'
            ? await recoverAuctionCommand(
                sessionId,
                command,
                abort.current.signal,
              )
            : await sendAuctionCommand(sessionId, command, abort.current.signal)
        if (receipt) await finish(receipt)
        else if (alive.current)
          setMessage(
            'Esito non ancora disponibile. Puoi verificare di nuovo o reinviare la stessa richiesta.',
          )
      } catch (error) {
        if (!alive.current) return
        if (
          error instanceof ApiError &&
          error.status >= 400 &&
          error.status < 500
        ) {
          if (mode === 'initial' && error.status !== 408) {
            storePending(userId, sessionId, null)
            setPending(null)
          }
          setMessage(
            mode === 'initial'
              ? errorMessage(error)
              : 'La richiesta originale resta in attesa di verifica. ' +
                  errorMessage(error),
          )
          if (error.status === 401 || error.status === 403)
            await client.invalidateQueries({
              queryKey: authQueryOptions().queryKey,
            })
          await refresh()
        } else
          setMessage(
            'La conferma non è arrivata. Verifica l’esito prima di una nuova operazione.',
          )
      } finally {
        lock.current = false
        if (alive.current) setBusy(false)
      }
    },
    [client, connected, finish, refresh, sessionId, userId],
  )
  const send = async (kind: CommandKind, values: Record<string, unknown>) => {
    if (lock.current || pending || !connected) return
    const command = pendingCommandSchema.parse({
      kind,
      body: { ...values, requestId: crypto.randomUUID() },
    })
    try {
      storePending(userId, sessionId, command)
    } catch {
      setMessage(
        'Impossibile conservare la richiesta in questo browser. Controlla che l’archiviazione del sito sia abilitata.',
      )
      return
    }
    setPending(command)
    setMessage('')
    await run(command, 'initial')
  }
  useEffect(() => {
    if (!connected) return
    const timer = setTimeout(() => {
      const saved = readPending(userId, sessionId)
      if (saved) void run(saved, 'recover')
    }, 0)
    return () => clearTimeout(timer)
  }, [connected, run, userId, sessionId])
  return {
    pending,
    busy,
    message,
    send,
    recover: () => pending && run(pending, 'recover'),
    retry: () => pending && run(pending, 'retry'),
  }
}
