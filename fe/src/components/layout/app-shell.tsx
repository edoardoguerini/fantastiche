import { createContext, useContext, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Link } from '@tanstack/react-router'
import { Brand } from '@/components/common/brand'

const HeaderSlot = createContext<HTMLDivElement | null>(null)

export function AppHeaderContent({ children }: { children: ReactNode }) {
  const target = useContext(HeaderSlot)
  return target ? createPortal(children, target) : null
}

export function AppShell({
  account,
  children,
}: {
  account: ReactNode
  children: ReactNode
}) {
  const [headerSlot, setHeaderSlot] = useState<HTMLDivElement | null>(null)
  return (
    <HeaderSlot.Provider value={headerSlot}>
      <div className="app-shell">
        <a className="skip-link" href="#main-content">
          Vai al contenuto
        </a>
        <header className="app-header">
          <Link
            to="/leghe"
            className="app-brand"
            aria-label="Fantastiche, le leghe"
          >
            <Brand compact />
            <span>Fantastiche</span>
          </Link>
          {account}
          <div className="app-header-slot" ref={setHeaderSlot} />
        </header>
        <main id="main-content">{children}</main>
      </div>
    </HeaderSlot.Provider>
  )
}
