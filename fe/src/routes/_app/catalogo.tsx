import { createFileRoute, redirect } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { CatalogPage } from '@/features/catalog'

export const Route = createFileRoute('/_app/catalogo')({
  beforeLoad: async ({ context }) => {
    const user = await context.queryClient.fetchQuery(authQueryOptions())
    if (!user) throw redirect({ to: '/login' })
    if (!user.isSuperAdmin) throw redirect({ to: '/leghe' })
  },
  head: () => ({ meta: [{ title: 'Listoni | Fantastiche' }] }),
  component: CatalogPage,
})
