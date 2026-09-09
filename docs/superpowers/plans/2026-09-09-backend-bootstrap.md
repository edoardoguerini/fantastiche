# Piano operativo primo incremento backend

> Esecuzione autonoma autorizzata dall’utente. Skill: superpowers:subagent-driven-development per compiti delegabili e review; coordinamento locale delle dipendenze.

**Obiettivo:** backend avviabile e flusso lega/invito/adesione verificato su SQL Server.
**Architettura:** struttura ACKSD con feature verticali, Identity/EF Core, boundary HTTP sottili, worker email separato.
**Stack:** .NET 10, SQL Server, Identity, EF Core, Dapper per coda/letture, xUnit, FluentValidation.
**Specifica:** [design](../specs/2026-09-09-backend-bootstrap-design.md).

## Vincoli globali

Solo be e documentazione; preservare preparazione locale; nessuna modifica ACKSD, invio esterno, push o deploy. Prose italiane, codice inglese. Branch `feature/be-bootstrap` nel checkout corrente per mantenere tutti i documenti non tracciati.

## Task 1: solution e contratti

- [x] Creare `be/Fantastiche.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, progetti src/tests e manifest dotnet-ef.
- [x] Core contiene RequestContext(Guid? UserId, bool IsSuperAdmin, string CorrelationId), DomainException(code,status), email contracts.
- [x] Infrastructure/Common contiene IRequest<T>, IRequestHandler<TRequest,TResponse>, IRequestPublisher.SendAsync<TRequest,TResponse> e QueryAsync, dispatcher DI. Modelli e DbContext restano in Infrastructure.
- [x] Restore e build; test di dispatch e envelope.

## Task 2: persistenza e inviti

- [x] Modelli e configurazioni per Identity, Leagues, LeagueSeasons, LeagueMembers, Teams, TeamMembers, LeagueInvitations, EmailMessages; migrazione iniziale.
- [x] Test SQL dei casi d’uso e regressioni della coda; ordine effettivo documentato nell’esito.
- [x] Use-case CreateLeague, GetLeague, InviteMember, GetInvitation, AcceptInvitation, RevokeInvitation, ResendInvitation. Context derivato dal server; errori codificati e transazioni atomiche.
- [x] Verificare scenari design, unicità, rollback, isolamento e replay.

## Task 3: host HTTP e autenticazione

- [x] `Application/Infrastructure`: moduli, envelope, exception handler, CSRF, correlation e rate limiting; `Modules/Auth`, `Modules/Leagues`, `Modules/Invitations` con validator dedicati.
- [x] Login/logout/me/antiforgery con cookie Identity, nessun endpoint di registrazione autonoma; bootstrap SuperAdmin CLI.
- [x] Test HTTP reali: anonimo 401, CSRF 400, privilegi 403, JSON invalidi 400, successo con envelope e stato appropriati.

## Task 4: email e scheduler

- [x] Core/Email, Gateways/Mailgun e LocalEmailSender; Infrastructure/Emails dispatcher e lease; Scheduler/Jobs worker sottile.
- [x] Test mittente HTTP simulato, acquisizione concorrente, retry, revoca/scadenza e payload protetto; nessun invio reale.

## Task 5: ambiente e consegna

- [x] Compose dedicato, Dockerfile API/Scheduler, `.env.example`, script setup locale e justfile riproducibili.
- [x] Restore, build Release, test unit/integration su SQL Server, migrazione e modello, avvio host/worker e health.
- [x] Review indipendente; correggere difetti e ripetere verifiche pertinenti.
- [x] Aggiornare README, regole e handoff con implementazione reale, comandi verificati e limiti; preservare frontend.

## Esito e scostamenti dal metodo

Primo incremento completato; risultati dettagliati nel [passaggio](../../workflow/backend-handoff.md). Esecuzione nel checkout su branch feature per preservare i file preparatori non tracciati. I test dei flussi sono stati completati durante l’implementazione; i due difetti della coda hanno una regressione riprodotta prima della correzione. Non si dichiara un ciclo TDD preventivo per ogni file di bootstrap. Nessun commit automatico della preparazione preesistente, né push/deploy.
