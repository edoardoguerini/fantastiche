import { useEffect, useId, useRef, useState } from 'react'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'

export function InvitationActionsMenu({
  name,
  disabled,
  onResend,
  onRevoke,
}: {
  name: string
  disabled: boolean
  onResend: () => void
  onRevoke: () => void
}) {
  const [open, setOpen] = useState(false)
  const id = useId()
  const root = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  useEffect(() => {
    if (!open) return
    const dismiss = (event: PointerEvent) => {
      if (!root.current?.contains(event.target as Node)) setOpen(false)
    }
    document.addEventListener('pointerdown', dismiss)
    return () => document.removeEventListener('pointerdown', dismiss)
  }, [open])
  const choose = (action: () => void) => {
    setOpen(false)
    trigger.current?.focus()
    action()
  }
  return (
    <div
      className="invitation-menu"
      ref={root}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false)
      }}
      onKeyDown={(event) => {
        if (event.key === 'Escape' && open) {
          event.preventDefault()
          event.stopPropagation()
          setOpen(false)
          trigger.current?.focus()
        }
      }}
    >
      <Button
        ref={trigger}
        variant="ghost"
        className="size-11 rounded-full p-0"
        aria-label={`Azioni invito ${name}`}
        aria-expanded={open}
        aria-controls={open ? id : undefined}
        disabled={disabled}
        onClick={() => setOpen(!open)}
      >
        <Icon name="ellipsis-vertical" variant="solid" />
      </Button>
      {open && (
        <div id={id} className="invitation-menu-content">
          <button
            type="button"
            disabled={disabled}
            onClick={() => choose(onResend)}
          >
            Reinvia invito
          </button>
          <button
            type="button"
            className="invitation-menu-danger"
            disabled={disabled}
            onClick={() => choose(onRevoke)}
          >
            Revoca invito
          </button>
        </div>
      )}
    </div>
  )
}
