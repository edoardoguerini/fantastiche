import { useNavigate } from '@tanstack/react-router'
import { Brand } from '@/components/common/brand'
import { LoginForm } from './login-form'

export function LoginPage() {
  const navigate = useNavigate()
  return (
    <main className="login-page" id="main-content">
      <div className="login-container">
        <Brand />
        <section className="login-panel" aria-labelledby="login-title">
          <header className="login-heading">
            <h1 id="login-title">Bentornato in campo.</h1>
            <p>Accedi e ritrova la tua lega.</p>
          </header>
          <LoginForm
            onSuccess={() => navigate({ to: '/leghe', replace: true })}
          />
          <div className="login-invitation">
            <p>È la tua prima volta?</p>
            <span>
              Apri l’invito ricevuto via email per attivare il tuo account.
            </span>
          </div>
        </section>
        <p className="login-footer">Il fantacalcio, insieme.</p>
      </div>
    </main>
  )
}
