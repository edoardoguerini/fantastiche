import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { authQueryOptions } from '@/features/auth'
import { ApiError, errorMessage } from '@/lib/api/error'

export function useCatalogAction(userId: string) {
  const client = useQueryClient()
  const lock = useRef(false)
  const controller = useRef<AbortController | null>(null)
  const mounted = useRef(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
      controller.current?.abort()
    }
  }, [])
  const isCurrent = () =>
    mounted.current &&
    client.getQueryData(authQueryOptions().queryKey)?.id === userId
  const run = async <T>(
    operation: (signal: AbortSignal) => Promise<T>,
    onSuccess: (result: T) => void | Promise<void>,
  ) => {
    if (lock.current || !isCurrent()) return
    lock.current = true
    setBusy(true)
    setError('')
    controller.current = new AbortController()
    try {
      const result = await operation(controller.current.signal)
      if (isCurrent()) await onSuccess(result)
    } catch (failure) {
      if (!isCurrent()) return
      setError(
        failure instanceof ApiError &&
          (failure.status === 0 || failure.status >= 500)
          ? 'La conferma non è arrivata. Aggiorna i listoni per verificare l’esito prima di riprovare.'
          : errorMessage(failure),
      )
      if (failure instanceof ApiError && [401, 403].includes(failure.status))
        await client.invalidateQueries({
          queryKey: authQueryOptions().queryKey,
        })
    } finally {
      lock.current = false
      if (mounted.current) setBusy(false)
    }
  }
  return { busy, error, run, isCurrent }
}
