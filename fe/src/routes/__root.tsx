import type { QueryClient } from '@tanstack/react-query'
import {
  createRootRouteWithContext,
  HeadContent,
  Link,
  Outlet,
  Scripts,
  useRouter,
} from '@tanstack/react-router'
import { ErrorState } from '@/components/common/page-state'
import { PwaControls } from '@/components/common/pwa-controls'
import '@fontsource-variable/sora/wght.css'
import '@/assets/fontawesome/css/all.css'
import '@/styles/globals.css'

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()(
  {
    headers: () => ({ 'Cache-Control': 'private, no-store', Vary: 'Cookie' }),
    head: () => ({
      meta: [
        { charSet: 'utf-8' },
        {
          name: 'viewport',
          content: 'width=device-width, initial-scale=1, viewport-fit=cover',
        },
        { title: 'Fantastiche' },
        { name: 'theme-color', content: '#0c0c0e' },
        { name: 'apple-mobile-web-app-capable', content: 'yes' },
        {
          name: 'apple-mobile-web-app-status-bar-style',
          content: 'black-translucent',
        },
        { name: 'apple-mobile-web-app-title', content: 'Fantastiche' },
        {
          name: 'description',
          content: 'La tua lega, la tua stagione. Fantastiche Fantacalcio.',
        },
      ],
      links: [
        { rel: 'icon', type: 'image/png', href: '/brand/fantastiche-logo.png' },
        { rel: 'manifest', href: '/manifest.webmanifest' },
        {
          rel: 'apple-touch-icon',
          sizes: '180x180',
          href: '/pwa/apple-touch-icon.png',
        },
      ],
    }),
    component: () => (
      <>
        <Outlet />
        {import.meta.env.PROD && <PwaControls />}
      </>
    ),
    shellComponent: ({ children }) => (
      <html lang="it" className="dark">
        <head>
          <HeadContent />
        </head>
        <body>
          {children}
          <Scripts />
        </body>
      </html>
    ),
    errorComponent: function RouteError({ error, reset }) {
      const router = useRouter()
      return (
        <ErrorState
          error={error}
          retry={() => {
            reset()
            void router.invalidate()
          }}
        />
      )
    },
    notFoundComponent: () => (
      <main className="page-state">
        <h1>Questa pagina non è in campo</h1>
        <p>Il collegamento potrebbe non essere più valido.</p>
        <Link to="/leghe" className="back-link">
          Vai alle leghe
        </Link>
      </main>
    ),
  },
)
