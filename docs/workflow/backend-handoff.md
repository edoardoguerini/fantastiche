# Ripartenza backend — bootstrap, catalogo e aste

Aggiornato il 9 settembre 2026. L’utente ha autorizzato esecuzione autonoma, solo backend, con struttura file, Docker e comandi just come ACKSD.

## Workspace e stato

Repository: `/Users/edoardoguerini/Documents/Lavoro/Fantastiche/applicazione/fantastiche`, branch `feature/be-bootstrap`. Preparazione precedente preservata: molti file erano già non tracciati; non confondere HEAD con lo stato su disco. Nessun push, merge o deploy eseguito. ACKSD in `/Users/edoardoguerini/Documents/Lavoro/ACKS/app` consultato in sola lettura.

## Implementato

- Solution .NET 10 con Core, Gateways, Infrastructure, Application e Scheduler; namespace, moduli HTTP e feature verticali secondo ACKSD. Pacchetti centralizzati, EF/Identity10.0.9, SDK minimo10.0.202 con rollForward latestFeature. Pin Microsoft.OpenApi2.7.5 per advisory verificato al restore.
- Identity, cookie HttpOnly8h senza sliding, antiforgery esplicito su tutte le mutazioni `/api` compreso login, logout e Me, lockout, rate limiting, forwarding da proxy fidati. IRequestPublisher risolve gli handler mediante DI; nessun MediatR.
- Bootstrap SuperAdmin tramite comando esplicito, password da configurazione locale, nessun default e nessuna sovrascrittura di account esistenti. Non è stato creato un SuperAdmin applicativo per l’utente: i test usano account fittizi in database temporanei rimossi alla fine.
- Schema e migrazione InitialOnboarding: Identity, leghe/stagioni, membership, squadre, inviti e coda email. Chiavi UUIDv7, vincoli univoci e FK composte coerenti con lega/stagione.
- Catalogo: Players, Clubs, ListVersions e ListEntries; migrazione AddCatalog e riferimento nullable in LeagueSeasons. Importazione in bozza riservata al SuperAdmin, pubblicazione esplicita, letture Dapper paginate e prima selezione del listone per lega. Dettaglio nella [guida catalogo](../../be/docs/domains/catalog.md).
- Creazione lega con organizzatore invitato, invito partecipante, preview, accettazione, revoca e reinvio. Account nuovo senza password, riuso account esistenti; attivazione/password/team/consumo atomici. Organizzatore e partecipante indipendenti. Inviti72h, hash token e payload coda protetto.
- Scheduler BackgroundService con polling5s, lease SQL2min, timeout30s, massimo5 tentativi e backoff. Acquisizione compatibile con RCSI e isolamento esplicito; errori SQL lasciano il lease recuperabile. Adapter Mailgun e mittente locale su file riservati.
- Docker Compose root/include e be/base+watch, API/Scheduler in container con hot reload, quattro Dockerfile dev/runtime, justfile root e be, script locale per segreti e tooling. Dati/chiavi/output Mac/Linux isolati.

## Verifiche eseguite

- Test Release: 36 integrazione/adapter e 47 unitari, 83 totali passati, nessuno ignorato. SQL Server reale locale; database di test dedicati eliminati dalla fixture.
- Flusso HTTP completo: login admin → lega → attivazione organizzatore → invito partecipante → squadra → accesso → logout; CSRF, credenziali errate, JSON invalido, health e OpenAPI.
- Onboarding: utenti nuovi/esistenti, nessun reset da invito, preview senza consumo, reinvio/revoca/scadenza, replay autenticato, concorrenza inviti/accettazione, rollback password in caso di conflitto squadra, isolamento leghe.
- Coda: worker concorrenti con RCSI ON, retry temporanei/permanenti, revoca, recupero lease scaduto, errore SQL iniettato dopo invio con conservazione payload. Adapter Mailgun testato con HTTP simulato, nessun invio esterno.
- Migrazione generata e applicata al database locale Fantastiche; `just be migrate-check` conferma assenza modifiche pendenti. `just be lint` passa.
- `just up-all` e `just restart-all` verificati; immagini development e multistage API/Scheduler costruite. Porte API6060 e SQL14333, progetto Docker `fantastiche`.
- Review indipendente completata senza rilievi alta/media aperti; corretti lock RCSI, errori di persistenza nella coda e forwarding del proxy.

## Avvio e accesso

Vedi [setup](../../be/docs/getting-started/development-setup.md). Dal root: `just be env`, `just up-all`, `just ps-all`. API `http://localhost:6060`, Scalar `/scalar` (anche `/scalar/v1`), OpenAPI `/openapi/v1.json`, health `/health/ready`. Il database contiene lo schema, non credenziali utente predefinite. Per entrare, compilare Bootstrap__Email/Password nel proprio `be/.env` e lanciare `just be bootstrap-admin`.

Lo stack watch forza `Email__Provider=Local`: nessun messaggio Mailgun reale. Le chiavi e le mail locali sono sotto `be/.local` e non vanno in Git. Il form `/invito` non esiste ancora: token e adesione sono verificabili tramite API/test.

## Limiti concreti e incrementi successivi

- Frontend, immagini Blob, Bicep e deploy restano da implementare. Abbonamenti/pagamenti esclusi. Motore d’asta e SignalR sono descritti nell’incremento in fondo al documento.
- Configurazione di produzione: origin/proxy effettivi, certificato per key ring persistente condiviso, mittente/dominio Mailgun Fantastiche, credenziali e risorse Azure. Nessuna risorsa ACKSD copiata.
- Container SQL Server su Mac ARM è emulato; le prove locali non certificano supporto produzione o carico Azure DTU.
- Una formattazione massiva ha riprodotto un errore interno Roslyn in dotnet watch SDK10.0.301. Il compose recupera con restart on-failure fino a3 tentativi; documentato anche `just restart-all`. Non riguarda le immagini runtime.
- La gestione delle stagioni dopo la creazione iniziale, liste/paginazione e pannelli amministrativi verranno aggiunti con le relative feature; non sono presenti endpoint generici CRUD non concordati.
- Il catalogo Classic è implementato secondo lo [specifico incremento](../superpowers/specs/2026-09-09-catalogo-backend-design.md); le parti aggiuntive della proposta originaria (statistiche, provider multipli, media) restano successive. Il CSV originale è stato letto dal parser per verifica, senza copiarlo o importarlo nel database applicativo. Diritti d’uso di dati/immagini restano da verificare prima del riuso commerciale.

## Riferimenti

- [Design primo incremento](../superpowers/specs/2026-09-09-backend-bootstrap-design.md) e [piano](../superpowers/plans/2026-09-09-backend-bootstrap.md).
- [Regole backend](../../be/CLAUDE.md), [indice guide](../../be/docs/README.md), [inviti/email](../../be/docs/domains/invitations-email.md).
- [Specifica asta](../superpowers/specs/2026-09-09-asta-design.md), [catalogo](../superpowers/specs/2026-09-09-catalogo-e-database-proposta.md).

## Aggiornamento Scalar e porte

Su richiesta dell’utente, API development sulla porta 6060 e frontend futuro riservato su 6061; SQL resta 14333. Aggiornati Docker, healthcheck, `.env` locale, esempio e link inviti. Aggiunto Scalar.AspNetCore2.0.36 con tema Purple come ACKSD, esposto in Development a `/scalar` e `/scalar/v1`. Verificati nel browser il rendering e il caricamento OpenAPI; 6 test HTTP passati, inclusi 2 nuovi test che riproducevano il precedente404.

## Incremento catalogo

Proseguito su `feature/be-bootstrap` su indicazione esplicita dell’utente, senza nuovo branch. Parser CSV standard, nessuna nuova dipendenza NuGet. File originale verificato in sola lettura: 594 righe, 74 P / 213 D / 203 C / 104 A; righe dei test completamente sintetiche.

Il CSV importato conserva solo i campi verificati e l’hash del contenuto. Quotazioni, FVM, statistiche, flag ambiguo e immagini non vengono interpretati. Bozza consultabile prima della pubblicazione; confronto automatico delle differenze non ancora presente. Pubblicazioni successive conservano identità del giocatore e vecchi snapshot. Le leghe conservano la versione scelta; la sostituzione richiede il futuro flusso tra sessioni d’asta.

Verifiche aggiunte: 41 test parser, 7 SQL reali per import concorrente/idempotente, immutabilità, filtri/pagine, permessi Active/Pending, scope incrociato, selezione e timeout lock; 3 scenari HTTP catalogo per autenticazione/CSRF, validazione e flusso completo bozza → pubblicazione → selezione lega. Suite completa 83/83 verde. Review indipendente senza difetti dimostrati aperti: corretti max ID esterno, confronto stagione coerente con SQL, mapping del timeout lock e validazione del ruolo nell’handler.

La stagione è ancora il nome esplicito già presente in LeagueSeason, non una nuova tabella sportiva. La fonte corrente è FantacalcioCsv; nessun matching automatico tra provider. Nessuna modifica frontend, nessun download immagini, nessun invio esterno.

Verifica operativa dell’incremento: `just be fmt`, `just be lint`, `just be migrate-check` e `just be migrate` completati; applicata `20260909175605_AddCatalog` al database Fantastiche locale. `just up-all` completato con API/Scheduler/SQL attivi. Health live/ready e Scalar restituiscono 200; OpenAPI espone le sei operazioni del catalogo e la consultazione anonima restituisce 401.

Ricostruite anche le immagini runtime multistage API e Scheduler con tag locali `fantastiche-api:verify` e `fantastiche-scheduler:verify`; nessuna pubblicazione su registry.

## Incremento motore d’asta

Sempre su `feature/be-bootstrap`, implementata la [specifica motore](../superpowers/specs/2026-09-09-motore-asta-design.md). Sette tabelle con FK composte e migrazione `20260909185355_AddAuctionEngine`; indice filtrato sulle scadenze aperte per il polling. Le migrazioni precedenti mantengono gli ID originali.

Comandi Dapper per sessione, chiamata, offerta e controlli organizzatore; un lock SQL per stagione coordina tutte le scritture e le letture dello stato. Timer SQL, riserva budget per completare la rosa, vincoli di ruolo, ricevute accettate/rifiutate e chiusura atomica con un solo acquisto/addebito/movimento. L’ordine salta le rose complete; tutte complete termina la sessione. Il lavoratore recupera le scadenze anche dopo riavvio e attraversa con paginazione le aste non chiudibili, senza bloccare le altre stagioni.

Nove operazioni HTTP, Hub `/hubs/Auctions` di sola lettura, osservazione delle versioni SQL ogni 500 ms e controllo Origin/CORS esplicito. I permessi vengono ricontrollati e le notifiche fallite rimangono da ritentare. Ogni connessione osserva al massimo 16 sessioni. Il client recupera stato e ricevute dopo una riconnessione; non servono offerte offline né timer autorevoli nel browser. Dettagli nella [guida aste](../../be/docs/domains/auctions.md).

Verifica finale Release: **128/128 test passati**, 74 d’integrazione e 54 unitari, nessuno ignorato. Comprende i test precedenti e 19 scenari motore SQL, 10 query SQL, 3 flussi HTTP d’asta, 6 scenari SignalR/SQL e 7 test unitari registro/origini. I database di test sono temporanei; nessun dato sintetico viene importato nel database applicativo.

Review indipendente incrociata completata: corretti recupero oltre 50 aste non chiudibili, race del registro sottoscrizioni, ritentativo delle notifiche fallite, ruolo SuperAdmin revocato nei comandi e nelle letture/Hub, codici di errore SignalR e normalizzazione delle origini. Nessun difetto concreto segnalato rimasto aperto. I permessi amministrativi vengono ricontrollati nel database dentro la transazione, prima delle scritture.

`just be fmt`, `just be lint`, `just be migrate-check` e `just up-all` completati. Migrazione AddAuctionEngine applicata al database locale; verificata la cronologia con InitialOnboarding/AddCatalog originali. API e SQL healthy, Scheduler avviato. Health live/ready e Scalar restituiscono 200; OpenAPI espone nove operazioni Auctions. API/Hub anonimi restituiscono 401, Origin Hub non ammessa restituisce 403. Ricostruite anche le immagini runtime `fantastiche-api:verify` e `fantastiche-scheduler:verify`, senza pubblicazione su registry.

Restano da implementare il client dell’asta, i flussi amministrativi successivi e la sostituzione del listone tra sessioni. Non è stato eseguito un test di carico multi-istanza su Azure SQL DTU; le prove di concorrenza usano connessioni SQL indipendenti locali. `CloseOnAuthenticationExpiration` è configurato nell’Hub; la scadenza del cookie non è stata simulata accelerandone il tempo. Nessun frontend, invio email esterno o deploy effettuato. Dopo queste verifiche, l’utente ha autorizzato commit e push del lavoro su `origin/feature/be-bootstrap`.
