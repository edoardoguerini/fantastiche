import { createRouter } from '@tanstack/react-router'
import { setupRouterSsrQueryIntegration } from '@tanstack/react-router-ssr-query'
import { createAuthenticatedQueryClient } from '@/features/auth'
import { LoadingState } from '@/components/common/page-state'
import { routeTree } from './routeTree.gen'

export function getRouter() {
  const queryClient = createAuthenticatedQueryClient()
  const router = createRouter({
    routeTree,
    context: { queryClient },
    scrollRestoration: true,
    defaultPreload: 'intent',
    defaultPendingComponent: LoadingState,
  })
  setupRouterSsrQueryIntegration({ router, queryClient })
  return router
}
declare module '@tanstack/react-router' {
  interface Register {
    router: ReturnType<typeof getRouter>
  }
}
