import { createFileRoute, Outlet, redirect } from '@tanstack/react-router'
import { AccountMenu, authQueryOptions, SessionGuard } from '@/features/auth'
import { AppShell } from '@/components/layout/app-shell'

export const Route = createFileRoute('/_app')({
  beforeLoad: async ({ context }) => {
    if (!(await context.queryClient.fetchQuery(authQueryOptions())))
      throw redirect({ to: '/login' })
  },
  component: () => (
    <SessionGuard>
      <AppShell account={<AccountMenu />}>
        <Outlet />
      </AppShell>
    </SessionGuard>
  ),
})
