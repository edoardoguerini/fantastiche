import { Icon } from '@/components/common/icon'

// Due passi solo quando il login è davvero separato dalla conferma.
export function InvitationSteps({
  labels,
  current,
}: {
  labels: [string, string]
  current: 1 | 2
}) {
  return (
    <ol className="invitation-steps" aria-label="Passaggi">
      {labels.map((label, index) => {
        const number = (index + 1) as 1 | 2
        const state =
          number === current ? 'current' : number < current ? 'done' : 'todo'
        return (
          <li
            key={label}
            data-state={state}
            aria-current={state === 'current' ? 'step' : undefined}
          >
            <i aria-hidden="true">
              {state === 'done' ? <Icon name="check" /> : number}
            </i>
            <span>{label}</span>
          </li>
        )
      })}
    </ol>
  )
}
