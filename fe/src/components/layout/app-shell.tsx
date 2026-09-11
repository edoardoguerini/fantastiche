import { createContext, useContext, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Link } from '@tanstack/react-router'
import { Brand } from '@/components/common/brand'

const HeaderSlot = createContext<HTMLDivElement | null>(null)
const BrandSlot = createContext<HTMLSpanElement | null>(null)

export function AppBrandContent({ children }: { children: ReactNode }) {
  const target = useContext(BrandSlot)
  return target ? createPortal(children, target) : null
}

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
  const [brandSlot, setBrandSlot] = useState<HTMLSpanElement | null>(null)
  return (
    <HeaderSlot.Provider value={headerSlot}>
      <BrandSlot.Provider value={brandSlot}>
        <div className="app-shell">
          <a className="skip-link" href="#main-content">
            Vai al contenuto
          </a>
          <header className="app-header">
            <Link
              to="/leghe"
              className="app-brand"
              aria-label="Fantastiche, le leghe"
              aria-describedby="app-brand-context"
            >
              <Brand compact />
              <span>Fantastiche</span>
              <span
                id="app-brand-context"
                className="app-brand-context"
                ref={setBrandSlot}
              />
            </Link>
            {account}
            <div className="app-header-slot" ref={setHeaderSlot} />
          </header>
          <main id="main-content">{children}</main>
        </div>
      </BrandSlot.Provider>
    </HeaderSlot.Provider>
  )
}
