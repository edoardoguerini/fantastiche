# Fantastiche — Panoramica e regole condivise

Punto d’ingresso per Claude Code e documento condiviso anche con Codex/GPT tramite [AGENTS.md](AGENTS.md). Regole specifiche in [be/CLAUDE.md](be/CLAUDE.md) e [fe/CLAUDE.md](fe/CLAUDE.md).

## Prodotto e stato

Fantastiche gestisce aste Classic: account individuali, leghe, chiamata a turno, timer per giocatore da 5 a 30 secondi, rilanci concorrenti, assegnazione e budget. L’app è una PWA per telefono, tablet e desktop, pensata anche come futuro prodotto commerciale. Abbonamenti, pagamenti e progettazione commerciale sono fuori dalla prima versione.

Il backend contiene solution .NET 10, Identity, moduli HTTP, schema/migrazioni, inviti e coda email con Scheduler, catalogo Classic versionato, motore d’asta con ricevute e SignalR, Docker e comandi just. Verificare lo stato effettivo nel documento di passaggio; login frontend, creazione/consultazione leghe e sala d’asta realtime sono implementati, insieme ad attivazione degli inviti, gestione partecipanti e catalogo da UI. Storage locale Azurite con card dei giocatori e stemmi dei club è operativo; Bicep, immagine FE nginx e pipeline GitHub Actions sono predisposti in [infra](infra/README.md); il provisioning Azure e la PWA restano da eseguire.

Priorità corrente: frontend. L’utente ha approvato solo dark mode, palette globale nero/antracite e lilla ripresa dalla sala d’asta, login centrato con gradiente viola e form di creazione lega per SuperAdmin, ora implementato. Linee e bordi minimali, Font Awesome self-hosted come ACKSD. Primo incremento in [specifica frontend](docs/superpowers/specs/2026-09-09-frontend-bootstrap-design.md); avvio e verifiche nel [setup frontend](fe/docs/getting-started/development-setup.md). La sala d’asta è descritta nella [guida frontend](fe/docs/architecture/auction-room.md). Lo stato backend è nel [documento di passaggio](docs/workflow/backend-handoff.md).

## Mappa

- `be/src/Fantastiche.Core`: contratti e tipi condivisi.
- `be/src/Fantastiche.Gateways`: adapter esterni.
- `be/src/Fantastiche.Infrastructure`: feature, handler, EF Core, Identity, Dapper.
- `be/src/Fantastiche.Application`: host e boundary HTTP/SignalR.
- `be/src/Fantastiche.Scheduler`: host BackgroundService per la coda email persistita.
- `fe/src`: routes, features, components, lib, styles.
- [be/docs](be/docs/README.md), [fe/docs](fe/docs/README.md): guide co-locate.
- [docs](docs/README.md): dominio, decisioni, sicurezza e workflow.
- [infra](infra/README.md): Bicep, moduli e procedura operativa per Azure Container Apps.

## Decisioni da preservare

- Azure SQL con modello DTU, ASP.NET Core Identity, EF Core e Dapper.
- Solo il SuperAdmin crea leghe. Il permesso di organizzatore appartiene a LeagueMembers; la partecipazione con una squadra passa da TeamMembers. Lo stesso utente può organizzare e giocare nella stessa lega.
- L’organizzatore inserisce nome/email e predispone account senza password se nuovo, membership Pending e invito personale. Il form consente attivazione o login dell’account esistente e scelta nome squadra. Logo per lega su Blob. Creazione e accodamento email atomici; lo Scheduler invia tramite Mailgun. Token casuale nell’email, hash per la verifica nel database. [Flusso](be/docs/domains/invitations-email.md).
- Teams sono le squadre della lega, Clubs le squadre reali; nessun prefisso Fantasy. Budget e rose configurabili per lega stagionale, default 500 crediti e 25 giocatori (3 P, 8 D, 8 C, 6 A).
- EF Core gestisce schema/migrazioni, Identity e configurazione ordinaria; Dapper letture delle schermate e transazioni critiche dell’asta.
- Un solo percorso di scrittura per operazione; SignalR pubblica dopo il commit.
- Ogni dato privato è riconducibile a lega e stagione. Nessun accesso a una lega senza permesso, anche con Dapper.
- Il catalogo è condiviso; disponibilità, ruoli e quotazioni dipendono dal listone/versione adottato.
- CSV Fantacalcio come prima fonte candidata; immagini verso Blob Storage. Bicep e pipeline sono in `infra/` e `.github/workflows/`; il trasferimento dati su Azure è una procedura operativa documentata in [infra](infra/README.md).
- TanStack Start con Vite, SPA iniziale implementata e API separate; non importare automaticamente il BFF/SSR di ACKS.
- Timer e accettazione offerte sono autorità del server; niente rilanci offline o aggiornamenti PWA che interrompano l’asta.

Specifiche: [asta](docs/superpowers/specs/2026-09-09-asta-design.md), [catalogo e database](docs/superpowers/specs/2026-09-09-catalogo-e-database-proposta.md).

## Convenzioni

- Documentazione, commenti, commit e PR in italiano; codice, tabelle e campi in inglese.
- Conventional commits, scope `be`, `fe`, `infra` o omesso. Niente firme AI o `Co-Authored-By` dell’assistente.
- Segreti, credenziali, dump, CSV importati e immagini scaricate non vanno nel repository. Eccezione autorizzata: il logo fornito dall’utente, convertito in `fe/public/brand/fantastiche-logo.png`, è un asset del prodotto. Anche la texture di erba fornita dall’utente, compressa in `fe/src/assets/pitch-grass.webp`, è un asset autorizzato, attualmente non utilizzato.
- Prima di una modifica leggere la guida dello stack e i file pertinenti. Aggiornare i docs quando cambia una convenzione.
- Non copiare configurazioni, identificativi Azure, dati, asset premium o dipendenze di ACKS senza necessità e verifica.
- Lo storage locale usa Azurite dedicato sulla porta 10010 con volume persistente; card e stemmi stanno in container separati, metadati su SQL. Importatori e istruzioni in [storage locale](be/docs/getting-started/local-storage.md). Nessun download del catalogo o backup locale va in Git.
- Le autorizzazioni già date dall’utente valgono per il lavoro concordato: non introdurre conferme ripetitive per modifiche locali reversibili.
- Workflow e verifiche in [development-process](docs/workflow/development-process.md); regole Git in [git-workflow](docs/workflow/git-workflow.md).
- I comandi `just` backend sono implementati secondo il riferimento ACKSD: [setup](be/docs/getting-started/development-setup.md).
