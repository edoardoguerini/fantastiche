import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { LeaguesPage } from '@/features/leagues'

export const Route = createFileRoute('/_app/leghe/')({
  validateSearch: z.object({
    page: z.coerce.number().int().min(1).max(100000).catch(1).default(1),
  }),
  head: () => ({ meta: [{ title: 'Le leghe | Fantastiche' }] }),
  component: function Page() {
    const { page } = Route.useSearch()
    return <LeaguesPage page={page} />
  },
})
