import { QueryClientProvider } from '@tanstack/react-query'
import { createRouter } from '@tanstack/react-router'
import { createAuthenticatedQueryClient } from '@/features/auth'
import { LoadingState } from '@/components/common/page-state'
import { routeTree } from './routeTree.gen'

export function getRouter() {
  const queryClient = createAuthenticatedQueryClient()
  return createRouter({
    routeTree,
    context: { queryClient },
    scrollRestoration: true,
    defaultPreload: 'intent',
    defaultPendingComponent: LoadingState,
    Wrap: ({ children }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  })
}
declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof getRouter>
  }
}
