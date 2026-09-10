import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { api } from '@/lib/api/client'
import { errorMessage } from '@/lib/api/error'
import { auctionKeys } from '../actions/auction.queries'
import { sessionSchema, type AuctionRoom } from '../types/auction.types'

export function TeamOrderEditor({
  order,
  onChange,
  disabled = false,
}: {
  order: { id: string; name: string }[]
  onChange: (order: { id: string; name: string }[]) => void
  disabled?: boolean
}) {
  const move = (index: number, offset: number) => {
    const next = [...order]
    ;[next[index], next[index + offset]] = [next[index + offset]!, next[index]!]
    onChange(next)
  }
  return (
    <ol className="team-order-editor">
      {order.map((team, index) => (
        <li key={team.id}>
          <span>{String(index + 1).padStart(2, '0')}</span>
          <strong>{team.name}</strong>
          <Button
            variant="ghost"
            disabled={disabled || index === 0}
            onClick={() => move(index, -1)}
            aria-label={`Sposta su ${team.name}`}
          >
            <Icon name="arrow-up" />
          </Button>
          <Button
            variant="ghost"
            disabled={disabled || index === order.length - 1}
            onClick={() => move(index, 1)}
            aria-label={`Sposta giù ${team.name}`}
          >
            <Icon name="arrow-down" />
          </Button>
        </li>
      ))}
    </ol>
  )
}
export function SessionSetup({
  room,
  userId,
}: {
  room: AuctionRoom
  userId: string
}) {
  const client = useQueryClient()
  const [order, setOrder] = useState(
    room.teams.map(({ id, name }) => ({ id, name })),
  )
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const create = async () => {
    if (busy) return
    setBusy(true)
    setError(null)
    try {
      sessionSchema.parse(
        await api.post('/Auctions/Sessions', {
          leagueId: room.leagueId,
          leagueSeasonId: room.leagueSeasonId,
          teamOrder: order.map((team) => team.id),
        }),
      )
    } catch (failure) {
      setError(failure)
    } finally {
      await client.invalidateQueries({
        queryKey: auctionKeys.room(userId, room.leagueId),
      })
      setBusy(false)
    }
  }
  return (
    <section className="auction-setup">
      <span className="auction-eyebrow">PRIMA DI COMINCIARE</span>
      <h2>{room.canManage ? 'Prepariamo l’asta.' : 'Ci siamo quasi.'}</h2>
      <p>
        {room.canManage
          ? 'Controlla l’ordine di chiamata delle squadre, poi apri la sessione.'
          : 'L’organizzatore avvierà la sessione. Questa pagina si aggiornerà automaticamente.'}
      </p>
      {!room.listVersionId && (
        <p className="auction-notice">
          Occorre prima associare un listone pubblicato alla stagione.
        </p>
      )}
      {room.canManage && (
        <>
          <TeamOrderEditor order={order} onChange={setOrder} disabled={busy} />
          {(order.length === 0 || order.length > 32) && (
            <p className="field-error">
              La sessione richiede da 1 a 32 squadre con partecipanti attivi.
            </p>
          )}
          {error !== null && (
            <p role="alert" className="form-error">
              {errorMessage(error)}
            </p>
          )}
          <Button
            disabled={
              busy || !room.listVersionId || !order.length || order.length > 32
            }
            onClick={() => void create()}
          >
            <Icon name="play" />
            {busy ? 'Apertura in corso…' : 'Apri la sessione d’asta'}
          </Button>
        </>
      )}
    </section>
  )
}
