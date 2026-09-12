import { createFileRoute } from '@tanstack/react-router'
import { AuctionRoomPage } from '@/features/auctions'

export const Route = createFileRoute('/_app/leghe/$leagueId/asta')({
  // Il guard del layout resta server-side; storage, audio e SignalR sono client.
  ssr: false,
  head: () => ({ meta: [{ title: 'Sala d’asta | Fantastiche' }] }),
  component: function Page() {
    const { leagueId } = Route.useParams()
    return <AuctionRoomPage leagueId={leagueId} />
  },
})
