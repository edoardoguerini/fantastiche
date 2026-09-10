import type { ReactNode } from 'react'
import { Link } from '@tanstack/react-router'
import { Brand } from '@/components/common/brand'

export function AppShell({
  account,
  children,
}: {
  account: ReactNode
  children: ReactNode
}) {
  return (
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
      </header>
      <main id="main-content">{children}</main>
    </div>
  )
}
