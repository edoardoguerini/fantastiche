import { createFileRoute, redirect } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { CreateLeaguePage } from '@/features/leagues'

export const Route = createFileRoute('/_app/leghe/nuova')({
  beforeLoad: async ({ context }) => {
    const user = await context.queryClient.fetchQuery(authQueryOptions())
    if (!user) throw redirect({ to: '/login' })
    if (!user.isSuperAdmin) throw redirect({ to: '/leghe' })
  },
  head: () => ({ meta: [{ title: 'Crea una lega | Fantastiche' }] }),
  component: CreateLeaguePage,
})
