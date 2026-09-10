# Fantastiche

Applicazione per aste di fantacalcio Classic, progettata per telefono, tablet e desktop come PWA e per una futura offerta commerciale.

## Stato

Backend implementato: solution .NET, Identity, leghe/stagioni, inviti, coda email, Scheduler, catalogo Classic versionato e motore d’asta con rilanci, aggiudicazione automatica e SignalR. Ambiente Docker e comandi `just` seguono ACKSD. Frontend implementato: login centrato in dark mode, sessione cookie, elenco/dettaglio/creazione leghe e sala d’asta realtime con turni, rilanci, rose e controlli organizzatore. Sono disponibili anche attivazione tramite invito, gestione partecipanti e inviti, importazione/anteprima/pubblicazione listoni e scelta del listone della lega. Card e stemmi sono serviti da Azurite locale. PWA e infrastruttura Azure restano da realizzare.

Avvio locale: `just be env`, `just up-all`. API e Scalar su `http://localhost:6060/scalar`; dettagli e credenziali locali nella [guida setup](be/docs/getting-started/development-setup.md). Frontend: `just fe install`, poi `just fe dev` su `http://localhost:6061/login`. Risultati nelle guide [backend](docs/workflow/backend-handoff.md) e [frontend](fe/docs/getting-started/development-setup.md).

## Orientamento

- [CLAUDE.md](CLAUDE.md): regole condivise e ingresso Claude.
- [AGENTS.md](AGENTS.md): ingresso Codex/GPT.
- [Documentazione trasversale](docs/README.md).
- [Backend](be/docs/README.md): ASP.NET Core, Identity, EF Core, Dapper, SignalR.
- [Frontend](fe/docs/README.md): TanStack Start, React, TypeScript, Vite e PWA.
- [Infrastruttura](infra/README.md): Azure SQL DTU e Blob Storage, tramite Bicep.
- [Adattamento del riferimento ACKS](docs/decisions/0001-struttura-e-convenzioni.md).

La struttura riprende ACKS: documentazione accanto allo stack e funzionalità organizzate per feature. Le risorse e i dati del progetto di riferimento restano separati.
