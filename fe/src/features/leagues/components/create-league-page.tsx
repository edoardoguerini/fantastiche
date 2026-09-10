import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { authQueryOptions } from '@/features/auth'
import { Icon } from '@/components/common/icon'
import { CreateLeagueForm } from './create-league-form'

export function CreateLeaguePage() {
  const { data: user } = useQuery(authQueryOptions())
  const navigate = useNavigate()
  if (!user?.isSuperAdmin)
    return (
      <div className="content-container">
        <p>La creazione delle leghe è riservata al SuperAdmin.</p>
        <Link className="back-link" to="/leghe">
          Torna alle leghe
        </Link>
      </div>
    )
  return (
    <div className="content-container create-league-container">
      <Link className="back-link" to="/leghe">
        <Icon name="arrow-left" />
        Torna alle leghe
      </Link>
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
