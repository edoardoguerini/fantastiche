# Aste Classic

Il motore vive in `Infrastructure/Auctions`: EF definisce lo schema, Dapper esegue comandi e query. Application espone HTTP, un Hub SignalR di sola lettura e i lavoratori di chiusura/notifica. Il [design dell’incremento](../../../docs/superpowers/specs/2026-09-09-motore-asta-design.md) descrive contratti e vincoli.

## Preparazione e chiamata

La stagione deve avere un listone pubblicato selezionato. Organizzatore attivo o SuperAdmin crea una sessione con l’ordine di 1–32 squadre della stagione, ciascuna con almeno un partecipante attivo. Può esistere una sola sessione Active/Paused per stagione. La sessione conserva la versione del listone e le squadre partecipanti.

La prima squadra con posti disponibili diventa chiamante; se tutte le rose sono complete, la creazione restituisce `auction.rosters_complete`. Qualunque membro attivo della squadra di turno può chiamare un giocatore disponibile. Il permesso di organizzatore o SuperAdmin, da solo, non consente di giocare.

La chiamata inserisce anche l’offerta iniziale di 1 credito. Il chiamante sceglie una durata di 5, 10, 15, 20, 25 o 30 secondi e da 1 a 10 incrementi distinti positivi. Gli incrementi servono alla futura interfaccia: l’API riceve sempre il totale assoluto dell’offerta.

## Offerte, tempo e aggiudicazione

Un’offerta deve superare l’importo corrente, provenire da una squadra partecipante con membership attiva e rispettare sia i posti del ruolo sia il budget. Il budget disponibile riserva 1 credito per ogni posto ancora da completare dopo l’acquisto.

Il tempo autorevole proviene da SQL Server, letto dopo l’acquisizione del lock della stagione. Un’offerta con `now >= deadline` è tardiva. Ogni offerta accettata riavvia l’intera durata; rifiuti e replay non spostano la scadenza.

Un lavoratore API verifica le scadenze ogni 500 ms, anche senza browser collegati. Sotto lo stesso lock usato dalle offerte ricontrolla la scadenza, assegna il giocatore, addebita il budget, registra il movimento e avanza il turno nella stessa transazione. Vincoli univoci e stato impediscono doppie aggiudicazioni. Il turno salta le rose complete; quando sono tutte complete termina la sessione. Dopo un riavvio vengono recuperate le aste già scadute.

Organizzatore o SuperAdmin può usare `Pause`, `Resume`, `SkipTurn`, `Reorder` e `Complete` tra giocatori. `Reorder` conserva le squadre e il chiamante corrente. `Complete` consente di terminare anticipatamente. Non è esposto un comando HTTP di aggiudicazione manuale.

## HTTP e ricevute

Tutte le route richiedono cookie autenticato; le mutazioni richiedono anche `X-XSRF-TOKEN`, ottenuto tramite `/api/Auth/Antiforgery` dopo il login.

| Metodo e percorso | Operazione |
|---|---|
| POST `/api/Auctions/Sessions` | Crea sessione con LeagueId, LeagueSeasonId e TeamOrder |
| GET `/api/Leagues/{leagueId}/Seasons/{leagueSeasonId}/Auction` | Recupera sessione attiva/pausa, oppure null |
| GET `/api/Leagues/{leagueId}/Seasons/{leagueSeasonId}/AuctionRoom` | Contesto sala, squadra personale, permessi e squadre attive della stagione |
| GET `/api/Auctions/Sessions/{sessionId}` | Stato, turno, ultima asta, budget, conteggi rosa e versione |
| POST `/api/Auctions/Sessions/{sessionId}/Players` | RequestId, PlayerId, DurationSeconds, Increments |
| POST `/api/Auctions/Sessions/{sessionId}/Bids` | RequestId, PlayerAuctionId, Amount |
| POST `/api/Auctions/Sessions/{sessionId}/Control` | RequestId, Action e TeamOrder facoltativo |
| GET `/api/Auctions/Sessions/{sessionId}/Commands/{requestId}` | Ricevuta del proprio comando |
| GET `/api/Auctions/Sessions/{sessionId}/Players/{playerAuctionId}/Bids` | Storico offerte accettate, page/pageSize |
| GET `/api/Auctions/Sessions/{sessionId}/Roster` | Rosa della stagione, teamId/page/pageSize facoltativi |
| GET `/api/Auctions/Sessions/{sessionId}/Catalog` | Listone della sessione con disponibilità stagionale, search/role/availableOnly/page/pageSize |

Per Start, Bid e Control il client genera un UUID RequestId e lo conserva fino al recupero dell’esito. Stesso utente, sessione, RequestId e payload restituiscono la ricevuta originaria. Il riuso con payload o tipo comando diverso restituisce 409. Sono persistiti anche i rifiuti di dominio; errori sintattici o accessi estranei alla lega possono precedere la ricevuta.

La risposta a un comando rifiutato ha lo status HTTP appropriato, `isSuccess=false` e il risultato persistito in `data`, compresi `accepted=false`, codice e versione. La GET della ricevuta ha esito positivo quando riesce a recuperarla, anche se il comando originario era rifiutato. Create non usa RequestId: il vincolo della sessione attiva e la relativa GET consentono il recupero.

Lo stato include l’ultima asta anche dopo la chiusura; `currentTeamId` è null per una sessione Completed. La rosa comprende gli acquisti dell’intera stagione, inclusi quelli di sessioni precedenti, con nomi e club dal listone originale di ciascun acquisto.

### Contesto sala e catalogo

`AuctionRoom` restituisce `{leagueId, leagueSeasonId, myTeamId, canManage, sessionId, listVersionId, teams}` anche prima della creazione della sessione. `myTeamId` proviene da TeamMembers con membership attiva e rimane null per chi organizza senza giocare. `canManage` richiede SuperAdmin verificato nel database oppure organizzatore attivo. Le `teams` hanno `{id, name, budget, goalkeepers, defenders, midfielders, forwards}` e comprendono le squadre della stagione con almeno un membro attivo, ordinate per nome/id. Budget e conteggi riflettono gli acquisti persistiti della stagione.

`sessionId` indica la sessione Active/Paused, altrimenti la Completed creata più recentemente, oppure null. `listVersionId` segue il listone della sessione selezionata; in assenza di sessioni segue la configurazione stagionale, eventualmente null. Questa lettura e il catalogo richiedono appartenenza attiva o SuperAdmin ancora valido: membri Pending, estranei e vecchie claim revocate non danno accesso. Le query Dapper acquisiscono il lock condiviso della stagione e ricontrollano l’autorizzazione dopo il lock.

`Catalog` restituisce `AuctionPage` (`items`, `page`, `pageSize`, `total`) con elementi `{playerId, name, role, clubName, isAvailable, teamId}` dal listone fissato nella sessione. `availableOnly` è true per default: esclude acquisti di tutta la stagione e la chiamata aperta, anche in un’altra sessione della stessa stagione. Con false include anche questi giocatori; `teamId` identifica soltanto la squadra acquirente, ed è null per la chiamata ancora aperta. `search` cerca nome e nome completo, rimuove gli spazi esterni e accetta fino a 200 caratteri, trattando letteralmente i caratteri jolly SQL. `role` accetta P/D/C/A senza distinzione maiuscole/minuscole. Default page=1 e pageSize=30, limiti rispettivamente 1–10000 e 1–100; ordine stabile per nome/id. Filtri e paginazione non validi restituiscono 400.

## SignalR e recupero client

Hub autenticato `/hubs/Auctions`. `WatchSession(sessionId)` restituisce lo stato autorevole e registra l’osservazione; `UnwatchSession(sessionId)` la rimuove. Ogni connessione può osservare al massimo 16 sessioni.

`AuctionChanged` comunica SessionId e Version. Il client ignora versioni già viste e ricarica lo stato; dopo una riconnessione ripete WatchSession e recupera le ricevute dei comandi con risposta incerta. Nessuna offerta offline e nessun timer client decide l’aggiudicazione.

Ogni istanza API osserva nel database le versioni delle proprie sottoscrizioni e ricontrolla i permessi prima della notifica. L’Hub è configurato per chiudere le connessioni alla scadenza dell’autenticazione; perdita della membership rimuove l’osservazione. Letture e comandi ricontrollano anche il ruolo SuperAdmin nel database: una vecchia claim non conserva privilegi revocati. Per questo protocollo basato su versioni SQL non è necessario un backplane SignalR.

Gli errori attesi dell’Hub contengono un codice stabile (`auth.forbidden`, `resource.not_found`, `auction.watch_limit`). Il protocollo SignalR può anteporre una descrizione del metodo e dell’eccezione; il codice rimane nel messaggio. I dettagli delle eccezioni tecniche restano nascosti.

`Cors:AllowedOrigins` contiene le origini esplicite ammesse, senza wildcard. In Development i default sono `http://localhost:6060` e `http://localhost:6061`. Un controllo Origin dedicato protegge anche l’upgrade WebSocket; CORS da solo non copre quel percorso. In produzione configurare l’origine effettiva del sito e usare HTTPS.

## Verifica locale

Si usano gli stessi Dockerfile, Compose e comandi `just` del backend: `just up-all`, API/Scalar su 6060, SQL su 14333. La migrazione aggiunge sette tabelle: AuctionSessions, CallOrderEntries, PlayerAuctions, Bids, RosterEntries, BudgetMovements e CommandReceipts.

I test d’integrazione usano SQL Server reale e dati sintetici in database temporanei. Coprono concorrenza, ricevute, timer, aggiudicazione, isolamento, HTTP e SignalR. Esiti e limiti della verifica corrente sono registrati nel [passaggio backend](../../../docs/workflow/backend-handoff.md); queste prove locali non misurano la capacità di carico su Azure SQL DTU.
