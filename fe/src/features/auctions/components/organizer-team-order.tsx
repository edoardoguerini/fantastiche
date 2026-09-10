import { useState, type ReactNode } from 'react'
import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import {
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import {
  roles,
  type AuctionSession,
  type RosterRules,
} from '../types/auction.types'

export function OrganizerTeamOrder({
  session,
  myTeamId,
  rules,
  disabled,
  onControl,
}: {
  session: AuctionSession
  myTeamId: string | null
  rules: RosterRules
  disabled: boolean
  onControl: (
    action: string,
    teamOrder?: string[],
    targetTeamId?: string,
  ) => Promise<void>
}) {
  const source = session.teamOrder.join(',')
  const [draft, setDraft] = useState<{ source: string; ids: string[] } | null>(
    null,
  )
  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, {
      coordinateGetter: sortableKeyboardCoordinates,
    }),
  )
  const ids = draft?.source === source ? draft.ids : session.teamOrder
  const teams = ids.flatMap(
    (id) => session.teams.find((team) => team.id === id) ?? [],
  )
  const dirty = ids.join(',') !== source
  const role = roles.find((value) => value.id === session.currentRole)
  const change = (next: string[]) => {
    if (!disabled) setDraft({ source, ids: next })
  }
  const move = (from: number, to: number) => {
    if (from < 0 || to < 0 || from >= ids.length || to >= ids.length) return
    const next = [...ids]
    const [id] = next.splice(from, 1)
    next.splice(to, 0, id!)
    change(next)
  }
  return (
    <section
      className="management-card management-order"
      aria-label="Ordine chiamate"
    >
      <header>
        <div>
          <h3>Ordine chiamate</h3>
          <p>Trascina o usa le frecce. Il turno corrente resta invariato.</p>
        </div>
        <div className="management-order-tools">
          <Button
            variant="outline"
            disabled={disabled || ids.length < 2}
            onClick={() => {
              const next = [...ids]
              for (let i = next.length - 1; i > 0; i--) {
                const j = Math.floor(
                  (crypto.getRandomValues(new Uint32Array(1))[0]! / 2 ** 32) *
                    (i + 1),
                )
                ;[next[i], next[j]] = [next[j]!, next[i]!]
              }
              change(next)
            }}
          >
            Mescola
          </Button>
          <Button
            variant="outline"
            disabled={disabled || ids.length < 2}
            onClick={() => change([...ids].reverse())}
          >
            Inverti
          </Button>
        </div>
      </header>
      <DndContext
        key={`${source}:${disabled}`}
        sensors={sensors}
        collisionDetection={closestCenter}
        accessibility={{
          screenReaderInstructions: {
            draggable:
              'Premi spazio per prendere la squadra, le frecce per spostarla, spazio per rilasciarla o Escape per annullare.',
          },
        }}
        onDragEnd={({ active, over }) => {
          if (over && active.id !== over.id)
            move(ids.indexOf(String(active.id)), ids.indexOf(String(over.id)))
        }}
      >
        <SortableContext items={ids} strategy={verticalListSortingStrategy}>
          <ol className="management-team-list">
            {teams.map((team, index) => {
              const roleFull = !role || team[role.field] >= rules[role.field]
              const current = team.id === session.currentTeamId
              return (
                <SortableTeam
                  key={team.id}
                  id={team.id}
                  name={team.name}
                  current={current}
                  disabled={disabled}
                >
                  <span className="management-team-avatar" aria-hidden="true">
                    {team.name
                      .split(/\s+/)
                      .slice(0, 2)
                      .map((word) => word[0])
                      .join('')}
                  </span>
                  <div className="management-team-info">
                    <h4>
                      {team.name} {team.id === myTeamId && <small>Tu</small>}{' '}
                      {current && (
                        <small className="is-current">Di turno</small>
                      )}
                    </h4>
                    <p>{team.budget} crediti</p>
                  </div>
                  <div className="management-team-actions">
                    <Button
                      variant="outline"
                      disabled={disabled || index === 0}
                      aria-label={`Sposta su ${team.name}`}
                      onClick={() => move(index, index - 1)}
                    >
                      <Icon name="arrow-up" variant="light" />
                    </Button>
                    <Button
                      variant="outline"
                      disabled={disabled || index === teams.length - 1}
                      aria-label={`Sposta giù ${team.name}`}
                      onClick={() => move(index, index + 1)}
                    >
                      <Icon name="arrow-down" variant="light" />
                    </Button>
                    <Button
                      variant="outline"
                      className="management-go"
                      disabled={disabled || current || roleFull}
                      aria-label={`Vai al turno di ${team.name}`}
                      onClick={() =>
                        void onControl('GoToTurn', undefined, team.id)
                      }
                    >
                      Vai al turno
                    </Button>
                  </div>
                </SortableTeam>
              )
            })}
          </ol>
        </SortableContext>
      </DndContext>
      {draft && draft.source !== source && (
        <p className="management-hint" role="status">
          L’ordine è stato aggiornato dal server.
        </p>
      )}
      {dirty && (
        <div className="management-save">
          <p>Modifiche da salvare</p>
          <Button
            variant="ghost"
            disabled={disabled}
            onClick={() => setDraft(null)}
          >
            Annulla modifiche
          </Button>
          <Button
            disabled={disabled}
            onClick={() => void onControl('Reorder', ids)}
          >
            Salva ordine
          </Button>
        </div>
      )}
    </section>
  )
}

function SortableTeam({
  id,
  name,
  current,
  disabled,
  children,
}: {
  id: string
  name: string
  current: boolean
  disabled: boolean
  children: ReactNode
}) {
  const {
    attributes,
    listeners,
    setNodeRef,
    setActivatorNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({
    id,
    disabled,
    transition: { duration: 200, easing: 'cubic-bezier(0.2, 0, 0, 1)' },
  })
  return (
    <li
      ref={setNodeRef}
      className="management-team"
      data-current={current}
      data-dragging={isDragging}
      style={{ transform: CSS.Transform.toString(transform), transition }}
    >
      <button
        ref={setActivatorNodeRef}
        type="button"
        className="management-grip"
        disabled={disabled}
        {...attributes}
        {...listeners}
        aria-label={`Trascina ${name}`}
      >
        <span aria-hidden="true">⠿</span>
      </button>
      {children}
    </li>
  )
}
