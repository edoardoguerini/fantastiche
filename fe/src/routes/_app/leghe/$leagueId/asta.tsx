import { createFileRoute } from '@tanstack/react-router'
import { AuctionRoomPage } from '@/features/auctions'

export const Route = createFileRoute('/_app/leghe/$leagueId/asta')({
  head: () => ({ meta: [{ title: 'Sala d’asta | Fantastiche' }] }),
  component: function Page() {
    const { leagueId } = Route.useParams()
    return <AuctionRoomPage leagueId={leagueId} />
  },
})
