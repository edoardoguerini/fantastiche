import { createFileRoute } from '@tanstack/react-router'
import { LeaguePage } from '@/features/leagues'

export const Route = createFileRoute('/_app/leghe/$leagueId/')({
  head: () => ({ meta: [{ title: 'La lega | Fantastiche' }] }),
  component: function Page() {
    const { leagueId } = Route.useParams()
    return <LeaguePage leagueId={leagueId} />
  },
})
