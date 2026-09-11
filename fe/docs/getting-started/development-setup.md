# Setup frontend

Primo incremento eseguibile: login centrato in dark mode, sessione cookie e consultazione delle leghe e della loro configurazione. Stack TanStack Start in SPA, React, TypeScript strict, Vite, Query, Form/Zod, Tailwind e primitives compatibili con shadcn. Versioni fissate nel package e nel lockfile, TypeScript 6.0.3 compatibile con il parser ESLint.

## Avvio locale

Prerequisiti: Node >=22.12, pnpm 10.33.1 e backend locale avviato.

Dal root:

```sh
just up-all       # backend Docker: API 6060 e SQL 14333
just fe install   # dipendenze dal lockfile
just fe dev       # frontend Vite: http://localhost:6061/login
```

`just up-all` continua a gestire il solo backend; Vite gira sull’host nel secondo terminale. Non occorre copiare `.env` per lo sviluppo locale. `VITE_API_BASE_URL` configura l’origin pubblico delle API, senza `/api` finale. Default sviluppo: `http://localhost:6060`; default build: stessa origin del sito. Solo configurazione pubblica nelle variabili `VITE_*`.

Il database iniziale non contiene account predefiniti. Per entrare con un account amministrativo seguire [bootstrap SuperAdmin](../../../be/docs/getting-started/development-setup.md#configurazione-e-superadmin), senza riutilizzare credenziali di ACKSD. I test automatici usano database temporanei; gli account locali per le prove manuali vengono creati esplicitamente tramite bootstrap. Nessun account demo viene aggiunto automaticamente e nessuna credenziale va nel repository.

## Flusso e API

- `/` porta alle leghe oppure al login se la sessione non è attiva.
- `/login`: email, password, mostra/nascondi password, errori e blocco degli invii doppi.
- `/leghe`: elenco paginato (20 elementi) delle leghe con membership Active; SuperAdmin vede tutte le leghe. Un elemento per lega con la stagione corrente, selezionata come nel dettaglio backend tramite `LeagueSeasons.Id DESC`.
- `/leghe/$leagueId`: configurazione della stagione corrente, budget iniziale e composizione della rosa.
- `/leghe/nuova`: form riservato al SuperAdmin, raggiungibile da “Crea lega”. Nome, stagione, organizzatore e regole configurabili. POST `/api/Leagues` con antiforgery; dopo la creazione aggiorna la cache dell’utente corrente e apre il dettaglio. Validazione client/server, blocco degli invii doppi e nessun retry automatico della creazione.

Client HTTP in `src/lib/api`, autenticazione in `features/auth`, leghe in `features/leagues`. Le API restituiscono `ApiResponse` (`isSuccess`, `data`, `errors`). GET `/api/Leagues?page=1&pageSize=20` restituisce `data.items`, `totalCount`, `page`, `pageSize`; `pageSize` massimo 100.

Il browser invia il cookie HttpOnly con `credentials: include`; prima di login/logout richiede `/api/Auth/Antiforgery` e invia `X-XSRF-TOKEN`. Nessun token persistito nel browser. Il cambio account/logout cancella richieste e cache. Una risposta 401 delle query private torna al login; una rete irraggiungibile mostra un errore con Riprova. Il backend resta autorevole per autorizzazioni e validazione. In produzione frontend e API devono essere sullo stesso sito con HTTPS, con origini CORS esatte ammesse dal backend.

## Verifiche e build

```sh
just fe test       # Vitest + Testing Library
just fe lint       # TypeScript, ESLint/import boundaries, Prettier
just fe build      # build SPA e generazione della shell
just fe test-e2e   # Playwright, Chrome installato localmente
```

Playwright avvia Vite se necessario. I test browser usano risposte API controllate sulla porta 6060 e non richiedono account; i test HTTP/SQL reali sono nel backend. Artefatti e screenshot sono in `fe/test-results`, ignorati da Git.

La build produce `dist/client/_shell.html` e gli asset statici. Il server di hosting deve servire prima gli asset esistenti, poi usare `_shell.html` come fallback delle route; `/api` resta destinato al backend. Nessun BFF o server function. [SPA mode ufficiale](https://tanstack.com/start/latest/docs/framework/react/guide/spa-mode).

Per provare una build localmente contro le API 6060:

```sh
cd fe
VITE_API_BASE_URL=http://localhost:6060 pnpm build
pnpm preview
```

Il prerender della shell avvia temporaneamente un server locale. Negli ambienti con sandbox occorre consentire l’apertura della porta locale per completare la build.

### Immagine di produzione

`just fe build-image` costruisce `fantastiche-fe:local` (build SPA + nginx non root su 8080); `just fe run-image` la avvia su `http://localhost:8080` inoltrando `/api` e `/hubs` all'API locale 6060. La configurazione nginx è `fe/nginx/default.conf.template`: `API_UPSTREAM` è l'unica variabile resa a runtime. Su Azure la stessa immagine riceve l'FQDN interno dell'API dal Bicep `infra/apps-fe.bicep`.

## Verifiche del 9 settembre 2026

- Frontend: 8 test unit/component e 8 test browser passati. Controllati desktop 1440px, mobile 390px e 320px, tastiera, errori, sessione e paginazione.
- Build SPA, typecheck, ESLint e Prettier verificati. Logo PNG originale preservato; margine bianco nascosto nella presentazione con clip CSS.
- Backend: 138 test passati (54 unitari, 84 integrazione), inclusi 10 nuovi casi per elenco e permessi; lint backend passato.
- API di sviluppo riavviata per caricare il nuovo endpoint: health ready 200, elenco anonimo 401.
- Smoke browser contro backend reale: login con credenziali inesistenti restituisce 401 dopo antiforgery valido. Il percorso completo di successo nel browser usa API simulate; quello HTTP di successo è coperto dai test backend con SQL reale.

## Incremento del 10 settembre 2026

Creazione lega collegata all’API esistente, bordi condivisi al 10% di opacità e Font Awesome Pro self-hosted come ACKSD, con default Classic Light. Verificati 19 test unit/component e 18 casi browser (il caso invii multipli rieseguito dopo la correzione della simulazione). Build, TypeScript, ESLint e Prettier passati. Screenshot controllati a 1440 e 390 px, assenza di overflow verificata anche a 320 px; caricamento webfont locale verificato nel browser. Le prove di creazione usano API simulate e non aggiungono leghe al database di sviluppo.

La creazione della lega accoda anche l’invito all’organizzatore. In sviluppo lo Scheduler salva l’email in `be/.local/mail`, senza inviarla esternamente. Il link `/invito` delle email è ora gestito dal frontend: attivazione di nuovi account o accesso e accettazione per account esistenti. Invito, reinvio e revoca sono disponibili agli organizzatori nel dettaglio lega. Recupero password, manifest/service worker PWA e deploy restano da implementare.

## Sala d’asta — 10 settembre 2026

Route `/leghe/$leagueId/asta`, accessibile dal dettaglio lega. Tabellone squadre, listone filtrabile, chiamata libera, timer e rilanci, rose/storico, preparazione e controlli organizzatore collegati alle API reali. Su mobile i rilanci restano in basso durante la chiamata. [Architettura e recupero](../architecture/auction-room.md).

Verifiche: 30 test unit/component, 29 test browser (suite completa 27 casi più 2 nuovi casi di preparazione), TypeScript, ESLint, Prettier e build SPA superati. Browser controllato a 1440, 390 e 320 px, incluse tastiera, offerta rifiutata, risposta persa, disconnessione e sessione scaduta. Review backend e frontend concluse senza P1/P2 residui. Backend: 54 unitari e 89 integrazione con SQL temporaneo superati.

In sviluppo sono stati predisposti tramite API otto partecipanti attivi, un listone di 594 calciatori reali dal CSV fornito e una sessione. Il catalogo inventato iniziale è stato sostituito su richiesta. Card e stemmi dei 20 club sono conservati nello storage Azurite locale. I dati restano nel database locale, senza credenziali o CSV nel repository. Smoke con due contesti Chrome separati contro API/SignalR reali: chiamata, rilancio, aggiudicazione dal worker, budget e rosa aggiornati su entrambi; nessun errore JavaScript o overflow. Le prove browser automatiche simulano HTTP e il protocollo SignalR e non scrivono nel database applicativo.

## Verifica finale — asta, inviti, catalogo e storage

Completati i form di adesione tramite invito, gestione partecipanti, reinvio/revoca e amministrazione del listone. Il dettaglio lega integra consultazione/scelta del listone e gestione partecipanti; `/catalogo` permette importazione, anteprima e pubblicazione al SuperAdmin. [Inviti](../architecture/invitations.md), [catalogo](../architecture/catalog.md) e [storage locale](../../../be/docs/getting-started/local-storage.md).

Verifica complessiva del 10 settembre: **44 test unitari/component frontend, 43 test browser, 68 test unitari backend e 95 integrazioni SQL**, tutti superati. Typecheck, ESLint, Prettier, build SPA e formattazione backend superati. Review dei permessi e dei flussi completata senza P1/P2 residui.

Smoke reale con due account: chiamata di Carnesecchi, rilancio a 2 crediti, chiusura automatica, budget e rosa aggiornati su entrambi i browser; card e stemma Atalanta caricati da Azurite. Verificati anche dettaglio lega e catalogo reali su mobile e desktop, senza errori JavaScript o overflow orizzontale. Nella demo, dopo la prova, la prossima chiamata spetta alla seconda squadra nell’ordine. Gli accessi sono stati consegnati fuori dal repository.

## Tema condiviso — 11 settembre 2026

La palette nero/antracite e lilla della sala d’asta è ora globale: login, inviti, elenco/dettaglio/creazione leghe e catalogo condividono superfici, testi, pulsanti e focus. Login e inviti usano un gradiente radiale CSS viola che sfuma nel nero; la texture del campo non viene più caricata. Il colore del browser (`theme-color`) segue lo sfondo globale.

Verificati 84 test browser, inclusi layout e navigazione da tastiera, build SPA, TypeScript ed ESLint. Controllate le schermate desktop e mobile e la formattazione dei file modificati. Il controllo Prettier globale segnala ancora il file preesistente `src/assets/animations/auction-confetti.json`, già minificato in Git e non modificato da questo intervento.
