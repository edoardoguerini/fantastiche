# Fantastiche

Applicazione per aste di fantacalcio Classic, progettata per telefono, tablet e desktop come PWA e per una futura offerta commerciale.

## Stato

Backend implementato: solution .NET, Identity, leghe/stagioni, inviti, coda email, Scheduler, catalogo Classic versionato e motore d’asta con rilanci, aggiudicazione automatica e SignalR. Ambiente Docker e comandi `just` seguono ACKSD. Frontend e infrastruttura Azure restano da realizzare.

Avvio locale: `just be env`, `just up-all`. API e Scalar su `http://localhost:6060/scalar`; dettagli e credenziali locali nella [guida setup](be/docs/getting-started/development-setup.md). Risultati delle verifiche nel [passaggio backend](docs/workflow/backend-handoff.md).

## Orientamento

- [CLAUDE.md](CLAUDE.md): regole condivise e ingresso Claude.
- [AGENTS.md](AGENTS.md): ingresso Codex/GPT.
- [Documentazione trasversale](docs/README.md).
- [Backend](be/docs/README.md): ASP.NET Core, Identity, EF Core, Dapper, SignalR.
- [Frontend](fe/docs/README.md): TanStack Start, React, TypeScript, Vite e PWA.
- [Infrastruttura](infra/README.md): Azure SQL DTU e Blob Storage, tramite Bicep.
- [Adattamento del riferimento ACKS](docs/decisions/0001-struttura-e-convenzioni.md).

La struttura riprende ACKS: documentazione accanto allo stack e funzionalità organizzate per feature. Le risorse e i dati del progetto di riferimento restano separati.
