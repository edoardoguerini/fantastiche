import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { Icon } from '@/components/common/icon'
import { Button } from '@/components/primitives/button'
import { CreateLeagueForm } from './create-league-form'

export function CreateLeaguePage() {
  const { data: user } = useQuery(authQueryOptions())
  const navigate = useNavigate()
  if (!user?.isSuperAdmin)
    return (
      <div className="content-container">
        <p>La creazione delle leghe è riservata al SuperAdmin.</p>
        <Button
          asChild
          variant="outline"
          className="league-back-link size-11 rounded-full p-0"
        >
          <Link
            to="/leghe"
            aria-label="Torna alle leghe"
            title="Torna alle leghe"
          >
            <Icon name="chevron-left" />
          </Link>
        </Button>
      </div>
    )
  return (
    <div className="content-container create-league-container">
      <Button
        asChild
        variant="outline"
        className="league-back-link size-11 rounded-full p-0"
      >
        <Link
          to="/leghe"
          aria-label="Torna alle leghe"
          title="Torna alle leghe"
        >
          <Icon name="chevron-left" />
        </Link>
      </Button>
      <header className="page-heading">
        <h1>Crea una lega</h1>
        <p>Una nuova stagione, le vostre regole.</p>
      </header>
      <CreateLeagueForm
        key={user.id}
        userId={user.id}
        onSuccess={async (league) => {
          await navigate({
            to: '/leghe/$leagueId',
            params: { leagueId: league.id },
          })
        }}
      />
    </div>
  )
}
