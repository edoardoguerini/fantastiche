import { useSyncExternalStore } from 'react'
import { getServerSnapshot, getSnapshot, subscribe } from './pwa-store'

export function usePwa() {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot)
}
