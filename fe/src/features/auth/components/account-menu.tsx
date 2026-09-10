import { useEffect, useId, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { ApiError, errorMessage } from '@/lib/api/error'
import { authQueryOptions } from '../actions/auth.queries'
import { logoutMutationOptions } from '../actions/auth.mutations'
import { replaceSession } from '../actions/auth.cache'
import '../account-menu.css'

export function AccountMenu() {
  const { data: user } = useQuery(authQueryOptions())
  const client = useQueryClient()
  const navigate = useNavigate()
  const logout = useMutation(logoutMutationOptions())
  const [error, setError] = useState<unknown>(null)
  const [open, setOpen] = useState(false)
  const container = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  const logoutItem = useRef<HTMLButtonElement>(null)
  const menuId = useId()
  const initials = user?.displayName.trim().split(/\s+/).filter(Boolean)
  const avatar = initials?.length
    ? `${initials[0]?.[0] ?? ''}${initials.length > 1 ? (initials.at(-1)?.[0] ?? '') : ''}`.toUpperCase()
    : '?'

  useEffect(() => {
    if (!open) return
    logoutItem.current?.focus()
    function dismiss(event: PointerEvent) {
      if (
        event.target instanceof Node &&
        !container.current?.contains(event.target)
      )
        setOpen(false)
    }
    function escape(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      event.preventDefault()
      setOpen(false)
      trigger.current?.focus()
    }
    document.addEventListener('pointerdown', dismiss)
    document.addEventListener('keydown', escape)
    return () => {
      document.removeEventListener('pointerdown', dismiss)
      document.removeEventListener('keydown', escape)
    }
  }, [open])
  async function signOut() {
    if (logout.isPending) return
    setError(null)
    try {
      await logout.mutateAsync()
    } catch (failure) {
      if (!(failure instanceof ApiError && failure.status === 401)) {
        setError(failure)
        return
      }
    }
    await replaceSession(client, null)
    await navigate({ to: '/login', replace: true })
  }
  return (
    <div
      className="account-menu"
      ref={container}
      onBlur={(event) => {
        if (
          event.relatedTarget &&
          !event.currentTarget.contains(event.relatedTarget)
        )
          setOpen(false)
      }}
    >
      <button
        type="button"
        className="account-trigger"
        ref={trigger}
        aria-label="Apri menu profilo"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen(!open)}
        onKeyDown={(event) => {
          if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault()
            setOpen(true)
          }
        }}
      >
        <span className="account-avatar" aria-hidden="true">
          {avatar}
        </span>
        <span className="account-identity">
          <strong>{user?.displayName}</strong>
          <span>{user?.email}</span>
        </span>
        <Icon name="chevron-down" className="account-chevron" />
      </button>
      {open && (
        <div
          id={menuId}
          role="menu"
          aria-label="Menu profilo"
          className="account-dropdown"
          onKeyDown={(event) => {
            if (['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) {
              event.preventDefault()
              logoutItem.current?.focus()
            }
          }}
        >
          <div
            className="account-identity account-dropdown-identity"
            role="presentation"
          >
            <strong>{user?.displayName}</strong>
            <span>{user?.email}</span>
          </div>
          <Button
            ref={logoutItem}
            role="menuitem"
            variant="ghost"
            className="account-logout"
            onClick={() => void signOut()}
            disabled={logout.isPending}
          >
            <Icon name="right-from-bracket" />
            {logout.isPending ? 'Uscita…' : 'Esci'}
          </Button>
          {error !== null && (
            <p role="alert" className="field-error">
              {errorMessage(error)}
            </p>
          )}
        </div>
      )}
    </div>
  )
}
