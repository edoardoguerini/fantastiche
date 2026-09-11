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
import '@fontsource-variable/sora/wght.css'
import '@/assets/fontawesome/css/all.css'
import '@/styles/globals.css'

export const Route = createRootRouteWithContext<{ queryClient: QueryClient }>()(
  {
    head: () => ({
      meta: [
        { charSet: 'utf-8' },
        {
          name: 'viewport',
          content: 'width=device-width, initial-scale=1, viewport-fit=cover',
        },
        { title: 'Fantastiche' },
        { name: 'theme-color', content: '#0c0c0e' },
        {
          name: 'description',
          content: 'La tua lega, la tua stagione. Fantastiche Fantacalcio.',
        },
      ],
      links: [
        { rel: 'icon', type: 'image/png', href: '/brand/fantastiche-logo.png' },
      ],
    }),
    component: () => (
      <html lang="it" className="dark">
        <head>
          <HeadContent />
        </head>
        <body>
          <Outlet />
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
