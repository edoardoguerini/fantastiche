import { useState } from 'react'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import type { AuctionSession } from '../types/auction.types'
import { TeamOrderEditor } from './session-setup'

export function OrganizerControls({
  session,
  disabled,
  onControl,
}: {
  session: AuctionSession
  disabled: boolean
  onControl: (action: string, teamOrder?: string[]) => Promise<void>
}) {
  const [confirm, setConfirm] = useState(false)
  const [editingOrder, setEditingOrder] = useState(false)
  const [order, setOrder] = useState(
    session.teams.map(({ id, name }) => ({ id, name })),
  )
  const open = session.currentAuction?.status === 'Open'
  if (session.status === 'Completed') return null
  return (
    <section className="organizer-controls">
      <div className="organizer-body">
        <p>
          {session.status === 'Paused'
            ? 'Sessione in pausa'
            : 'Sessione aperta'}
        </p>
        <p>I controlli sono disponibili tra un giocatore e il successivo.</p>
        <div className="organizer-buttons">
          <Button
            variant="outline"
            disabled={disabled || open}
            onClick={() =>
              void onControl(session.status === 'Paused' ? 'Resume' : 'Pause')
            }
          >
            <Icon name={session.status === 'Paused' ? 'play' : 'pause'} />
            {session.status === 'Paused' ? 'Riprendi' : 'Pausa'}
          </Button>
          <Button
            variant="outline"
            disabled={disabled || open}
            onClick={() => void onControl('SkipTurn')}
          >
            Salta turno
          </Button>
          <Button
            variant="outline"
            disabled={disabled || open}
            onClick={() => {
              setOrder(session.teams.map(({ id, name }) => ({ id, name })))
              setEditingOrder(!editingOrder)
            }}
          >
            Ordine chiamate
          </Button>
          <Button
            variant="ghost"
            disabled={disabled || open}
            onClick={() => setConfirm(true)}
          >
            Termina asta
          </Button>
        </div>
        {editingOrder && (
          <div>
            <TeamOrderEditor
              order={order}
              onChange={setOrder}
              disabled={disabled || open}
            />
            <Button
              disabled={disabled || open}
              onClick={() => {
                void onControl(
                  'Reorder',
                  order.map((team) => team.id),
                )
                setEditingOrder(false)
              }}
            >
              Salva ordine
            </Button>
          </div>
        )}
        {confirm && (
          <div className="auction-notice" role="alert">
            <p>Terminare questa sessione? Gli acquisti restano nelle rose.</p>
            <div className="organizer-buttons">
              <Button variant="ghost" onClick={() => setConfirm(false)}>
                Annulla
              </Button>
              <Button
                disabled={disabled || open}
                onClick={() => {
                  void onControl('Complete')
                  setConfirm(false)
                }}
              >
                Conferma conclusione
              </Button>
            </div>
          </div>
        )}
      </div>
    </section>
  )
}
