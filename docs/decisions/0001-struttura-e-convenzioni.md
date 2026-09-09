# ADR 0001 — Struttura e convenzioni da ACKS

Data: 9 settembre 2026. Stato: struttura e documentazione adottate su richiesta dell’utente; bootstrap applicativo successivo.

## Contesto

Riferimento locale letto: `/Users/edoardoguerini/Documents/Lavoro/ACKS/app`. L’utente richiede la stessa organizzazione dei docs per Claude/GPT e delle cartelle backend/frontend. ACKS è rimasto in sola lettura.

## Valutazione del riferimento

Valutazione documentale, non certificazione di build o audit del progetto ACKS:

| File | Comandi /20 | Architettura /20 | Pattern /15 | Sintesi /15 | Allineamento /15 | Operatività /15 | Totale |
| --- | --- | --- | --- | --- | --- | --- | --- |
| CLAUDE.md root | 18 | 18 | 14 | 11 | 12 | 13 | 86/100 |
| be/CLAUDE.md | 18 | 18 | 14 | 10 | 11 | 13 | 84/100 |
| fe/CLAUDE.md | 16 | 18 | 14 | 10 | 10 | 12 | 80/100 |

Tre ingressi CLAUDE individuati; nessun AGENTS.md trovato nella ricerca del riferimento. Media 83/100. Nessun file del riferimento da modificare: i tre ingressi richiedono adattamento nella destinazione.

Punti utili: docs co-locate, feature verticali, naming esplicito, guide operative, checklist review. Incongruenze osservate: project-structure descrive Gateways vuoto ma esistono adapter Azure Blob/Mailgun; vecchie indicazioni frontend/reviewer usano components/ui mentre la convenzione corrente è components/primitives; alcuni indici elencano guide non ancora presenti.

## Struttura adottata

- Radice `be/`, `fe/`, `infra/`, `docs/`, `.claude/agents/`.
- CLAUDE.md a root, be, fe; AGENTS.md agli stessi livelli rimandano alle medesime regole.
- Docs di stack divise in getting-started, architecture, conventions, domains dove utile, how-to e quality.
- Backend con Core, Gateways, Infrastructure, Application e spazio riservato Scheduler.
- Infrastructure con Domain/Persistence, use-case e Payloads per feature.
- Frontend con routes, features, components/primitives/common/layout, lib e styles.
- Checklist reviewer condivise, richiamabili anche senza strumenti proprietari.
- Convenzioni italiane per prose e commit, inglesi per codice e schema.

## Adattamenti necessari

- Prefisso `Fantastiche`; domini Leagues, Catalog, Teams, Auctions al posto del dominio aziendale.
- Isolation tramite League/LeagueSeason e membership, non copia di TenantId e bypass staff ACK.
- Dapper supportato esplicitamente; filtri/interceptor EF non si applicano al SQL Dapper.
- Storico offerte e budget preservato; niente soft-delete universale delle entità.
- Identity adottato; trasporto dell’autenticazione e sessioni ancora da progettare.
- TanStack Start SPA iniziale proposta: BFF/SSR del riferimento non adottati automaticamente.
- SignalR e PWA aggiunti alle convenzioni.
- NuGet centralizzato e pnpm mantenuti come convenzioni; versioni e librerie effettive si verificheranno al bootstrap. MediatR/Hangfire non introdotti automaticamente.
- Bicep predisposto documentalmente: nessun identificativo, credenziale, risorsa o pipeline ACKS copiato.
- Git Flow e work item Azure DevOps restano il modello di riferimento, da collegare al remoto Fantastiche; non sono un prerequisito inventato per questa preparazione locale.
- Niente font/icone premium, configurazioni personali Claude, hook eseguibili o vecchi piani di ACKS.
- Le vecchie skill che dipendono da implementazioni ACKS non vengono rese obbligatorie: le guide how-to sono direttamente leggibili da entrambi gli assistenti.

## Stato consegnato

Cartelle tracciabili tramite README/.gitkeep e documentazione adattata. Nessun .csproj, package.json, justfile, Dockerfile, migration o Bicep eseguibile: questi appartengono al bootstrap e al provisioning successivi.
