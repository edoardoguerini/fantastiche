import { createFileRoute } from '@tanstack/react-router'
import { InvitationPage } from '@/features/invitations'

export const Route = createFileRoute('/invito')({
  // Il token nel fragment è disponibile soltanto nel browser.
  ssr: false,
  head: () => ({
    meta: [
      { title: 'Invito | Fantastiche' },
      { name: 'referrer', content: 'no-referrer' },
      { name: 'robots', content: 'noindex, nofollow' },
    ],
  }),
  component: InvitationPage,
})
