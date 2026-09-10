import { createFileRoute, redirect } from '@tanstack/react-router'
import { LoginPage, authQueryOptions } from '@/features/auth'

export const Route = createFileRoute('/login')({
  beforeLoad: async ({ context }) => {
    if (await context.queryClient.fetchQuery(authQueryOptions()))
      throw redirect({ to: '/leghe' })
  },
  head: () => ({ meta: [{ title: 'Accedi | Fantastiche' }] }),
  component: LoginPage,
})
