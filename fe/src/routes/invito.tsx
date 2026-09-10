import { createFileRoute } from '@tanstack/react-router'
import { InvitationPage } from '@/features/invitations'

export const Route = createFileRoute('/invito')({
  head: () => ({
    meta: [
      { title: 'Invito | Fantastiche' },
      { name: 'referrer', content: 'no-referrer' },
      { name: 'robots', content: 'noindex, nofollow' },
    ],
  }),
  component: InvitationPage,
})
