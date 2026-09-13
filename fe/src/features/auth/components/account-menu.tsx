import { useEffect, useId, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useHydrated, useNavigate } from '@tanstack/react-router'
import { Button } from '@/components/primitives/button'
import { Icon } from '@/components/common/icon'
import { PwaInstallGuide } from '@/components/common/pwa-install-guide'
import { ApiError, errorMessage } from '@/lib/api/error'
import { promptInstall } from '@/lib/pwa/pwa-store'
import { usePwa } from '@/lib/pwa/use-pwa'
import { authQueryOptions } from '../actions/auth.queries'
import { logoutMutationOptions } from '../actions/auth.mutations'
import { replaceSession } from '../actions/auth.cache'
import '../account-menu.css'

function menuItems(menu: HTMLElement | null) {
  return Array.from(
    menu?.querySelectorAll<HTMLElement>('[role="menuitem"]') ?? [],
  )
}
function focusItem(menu: HTMLElement | null, index: number) {
  const all = menuItems(menu)
  all.at(index % all.length)?.focus()
}

export function AccountMenu() {
  const hydrated = useHydrated()
  const { data: user } = useQuery(authQueryOptions())
  const client = useQueryClient()
  const navigate = useNavigate()
  const logout = useMutation(logoutMutationOptions())
  const pwa = usePwa()
  const [error, setError] = useState<unknown>(null)
  const [open, setOpen] = useState(false)
  const [guide, setGuide] = useState(false)
  const container = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  const menu = useRef<HTMLDivElement>(null)
  const menuId = useId()
  const canInstall = pwa.enabled && !pwa.standalone
  const initials = user?.displayName.trim().split(/\s+/).filter(Boolean)
  const avatar = initials?.length
    ? `${initials[0]?.[0] ?? ''}${initials.length > 1 ? (initials.at(-1)?.[0] ?? '') : ''}`.toUpperCase()
    : '?'

  function close() {
    setOpen(false)
    setGuide(false)
  }

  useEffect(() => {
    if (!open) return
    focusItem(menu.current, 0)
    function dismiss(event: PointerEvent) {
      if (
        event.target instanceof Node &&
        !container.current?.contains(event.target)
      )
        close()
    }
    function escape(event: KeyboardEvent) {
      if (event.key !== 'Escape') return
      event.preventDefault()
      close()
      trigger.current?.focus()
    }
    document.addEventListener('pointerdown', dismiss)
    document.addEventListener('keydown', escape)
    return () => {
      document.removeEventListener('pointerdown', dismiss)
      document.removeEventListener('keydown', escape)
    }
  }, [open])
  async function install() {
    if (!pwa.canPrompt) {
      setGuide((current) => !current)
      return
    }
    const outcome = await promptInstall()
    if (outcome === 'accepted') close()
    else if (outcome === 'unavailable') setGuide(true)
  }
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
          close()
      }}
    >
      <button
        type="button"
        disabled={!hydrated}
        className="account-trigger"
        ref={trigger}
        aria-label="Apri menu profilo"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => (open ? close() : setOpen(true))}
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
          ref={menu}
          onKeyDown={(event) => {
            const all = menuItems(menu.current)
            const current = all.indexOf(document.activeElement as HTMLElement)
            const next = {
              ArrowDown: current + 1,
              ArrowUp: current - 1 + all.length,
              Home: 0,
              End: all.length - 1,
            }[event.key]
            if (next === undefined) return
            event.preventDefault()
            focusItem(menu.current, next)
          }}
        >
          <div
            className="account-identity account-dropdown-identity"
            role="presentation"
          >
            <strong>{user?.displayName}</strong>
            <span>{user?.email}</span>
          </div>
          {canInstall && (
            <>
              <Button
                role="menuitem"
                variant="ghost"
                className="account-menu-item"
                aria-expanded={pwa.canPrompt ? undefined : guide}
                onClick={() => void install()}
              >
                <Icon name="cloud-arrow-down" />
                Installa app
              </Button>
              {guide && (
                <div className="account-install-guide">
                  <PwaInstallGuide />
                </div>
              )}
            </>
          )}
          <Button
            role="menuitem"
            variant="ghost"
            className="account-menu-item"
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
