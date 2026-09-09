# Setup backend

Solution `.NET 10` in `be/Fantastiche.slnx`: Core, Gateways, Infrastructure, Application e Scheduler. Struttura file, Docker e ricette `just` seguono ACKSD. Frontend e Bicep non fanno parte di questo incremento.

## Prerequisiti

SDK indicato in `be/global.json`, Docker Desktop e `just`; Python 3 per caricare/generare la configurazione locale senza stampare credenziali. Le versioni NuGet sono in `Directory.Packages.props`, il tool EF è locale al repository.

Su Apple Silicon SQL Server gira in emulazione `linux/amd64` per lo sviluppo. Microsoft supporta i container SQL Server su Linux x86-64, non gli emulatori: [documentazione](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-docker-container-deployment?view=sql-server-ver17). Le verifiche locali non certificano prestazioni Azure/DTU.

## Comandi dal root

```sh
just be env          # genera be/.env con password SQL casuale
just be restore
just be up           # solo SQL Server; attende health
just be migrate      # applica la migration al DB locale
just up-all          # SQL + migrazione one-shot + API + Scheduler, hot reload
just ps-all
just logs-all
just down-all        # ferma senza eliminare volumi
```

API: `http://localhost:6060`; health `/health/live`, readiness `/health/ready`, Scalar `/scalar` (anche `/scalar/v1`), OpenAPI `/openapi/v1.json` in Development. SQL Server: `127.0.0.1,14333`, database Fantastiche. Nessun frontend viene avviato. Il compose usa progetto `fantastiche`, rete e volumi dedicati. Nessuna risorsa ACKSD riutilizzata.

Il motore d’asta espone API sotto `/api/Auctions/Sessions` e Hub autenticato `/hubs/Auctions`. Chiusura delle aste scadute e notifiche girano nell’API, ogni 500 ms, senza dipendere da un browser; Scheduler resta dedicato alle email. Dettagli e recupero dei comandi nella [guida aste](../domains/auctions.md).

`be/docker-compose.yml` contiene il database; `be/docker-compose.watch.yml` aggiunge migrazione, API e Scheduler; `docker-compose.dev.yml` li include dal root. I quattro Dockerfile corrispondono ad API/Scheduler in development e pubblicazione multistage. Gli output Linux di **tutti** i progetti sono isolati in volumi `.artifacts` distinti per container; quelli host restano in bin/obj. Cache NuGet condivisa, key ring e mail locali in `be/.local/`.

`just be watch` avvia lo stesso backend in foreground. L’API gira in container; build/test/tooling EF girano sull’host. `just --list` e `just --list be` elencano le ricette disponibili.

## Configurazione e SuperAdmin

`be/.env` è gitignored e non contiene password predefinite. `be/.env.example` descrive le variabili: non usare il placeholder come credenziale. `scripts/dev.py` carica `.env` e genera la connection string host; non stampa segreti e non usa eval/source della shell.

Il database iniziale non contiene account. Aggiungere a `be/.env` i propri valori `Bootstrap__Email`, `Bootstrap__Password` (almeno 12 caratteri, maiuscole/minuscole/numeri/simboli) e opzionalmente `Bootstrap__DisplayName`, poi eseguire:

```sh
just be bootstrap-admin
```

Il comando gira nel container API e rifiuta email di account esistenti: nessun reset o promozione implicita. Togliere la password di bootstrap dalla configurazione quando non serve più. Avvio normale e migrazione non creano utenti.

## Email e chiavi

Default `Email__Provider=Local`: nessun invio esterno; Scheduler deposita il contenuto email in file JSON `be/.local/mail`, con permessi riservati. Contengono link di attivazione: sono artefatti locali, non vanno nel repository. La destinazione del futuro form si configura in `Invitations__PublicBaseUrl`; il frontend non è implementato, quindi per ora usare il token nei test/API.

In database la coda conserva il payload protetto con Data Protection. API e Scheduler usano application name `Fantastiche` e lo stesso `DataProtection__KeyPath`; conservare le chiavi tra i riavvii. Dopo invio/annullamento/fallimento definitivo il payload viene cancellato. Fuori Development occorrono percorso chiavi persistente e certificato PFX (`DataProtection__CertificatePath`, password tramite configurazione segreta).

L’adapter Mailgun richiede regione EU/US, dominio, From, ApiKey e `Email__EnableExternalDelivery=true`. Lo stack watch forza il mittente Local; la configurazione di un ambiente reale è separata e non è stata eseguita.

## Test e manutenzione

```sh
just be build
just be test-unit
just be test-int
just be test
just be migrate-check
just be migrate-script
just be lint
```

I test di integrazione richiedono SQL Server acceso. Creano e rimuovono solo database `Fantastiche_Test_<guid>` sulla porta locale 14333; non usano il database applicativo. La coda viene provata anche con READ_COMMITTED_SNAPSHOT attivo come Azure SQL. Test e migrazioni non effettuano chiamate Mailgun reali.

`just be migrate-add Nome` crea una migrazione in `Infrastructure/Common/Persistence/Migrations`. `just be migrate-script` produce SQL idempotente in `.local`. Non sono previsti comandi reset distruttivi automatici.

Il documento [stato e verifiche](../../../docs/workflow/backend-handoff.md) registra i risultati effettivamente eseguiti.

## Proxy e hot reload

Dietro un reverse proxy impostare `ReverseProxy__KnownProxies__0` (e indici successivi) agli IP effettivi dei proxy fidati. Il backend accetta un solo salto X-Forwarded-For/Proto e applica il rate limiter dopo il forwarding. In assenza di configurazione valgono solo i proxy loopback del framework; non abilitare fiducia indiscriminata agli header inviati dal client.

Configurare le origini del sito in `Cors__AllowedOrigins__0` e indici successivi, con schema/host/porta e senza wildcard. I default Development sono `http://localhost:6060` e `http://localhost:6061`; fuori Development occorre configurare le origini effettive. La stessa lista protegge CORS con cookie e l’Origin delle connessioni WebSocket.

Durante la verifica, una modifica massiva tramite formatter ha fatto terminare `dotnet watch` per un errore interno Roslyn nell’SDK container 10.0.301. I container watch hanno `restart: on-failure:3` per recuperare con una compilazione pulita; `just restart-all` resta disponibile. Il reload di modifiche ordinarie resta attivo. Questo limite del tooling development non riguarda le immagini runtime.
