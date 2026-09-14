# Aste Classic

Il motore vive in `Infrastructure/Auctions`: EF definisce lo schema, Dapper esegue comandi e query. Application espone HTTP, un Hub SignalR di sola lettura e i lavoratori di chiusura/notifica. Il [design dell’incremento](../../../docs/superpowers/specs/2026-09-09-motore-asta-design.md) descrive contratti e vincoli.

## Preparazione e chiamata

La stagione deve avere un listone pubblicato selezionato. Organizzatore attivo o SuperAdmin crea una sessione con l’ordine di 1–32 squadre della stagione, ciascuna con almeno un partecipante attivo. Può esistere una sola sessione Active/Paused per stagione. La sessione conserva la versione del listone e le squadre partecipanti.

Le chiamate seguono sempre le fasi **P → D → C → A**. Si passa al ruolo successivo solo quando tutti i partecipanti hanno completato gli slot di quello corrente; i ruoli configurati a zero vengono saltati. La fase deriva dagli acquisti stagionali dei partecipanti, compresi quelli delle sessioni precedenti. La prima squadra con posti disponibili nel ruolo corrente diventa chiamante; se tutte le rose sono complete, la creazione restituisce `auction.rosters_complete`. Qualunque membro attivo della squadra di turno può chiamare un giocatore disponibile. Il permesso di organizzatore o SuperAdmin, da solo, non consente di giocare.

Il server rifiuta una chiamata fuori fase con `auction.wrong_role`, senza aprire un’asta o cambiare budget/versione. Le ricevute dei rifiuti mantengono la stessa idempotenza degli altri comandi. La chiamata inserisce anche l’offerta iniziale di 1 credito. Il chiamante sceglie una durata di 5, 10, 15, 20, 25 o 30 secondi e da 1 a 10 incrementi distinti positivi. Gli incrementi servono alla futura interfaccia: l’API riceve sempre il totale assoluto dell’offerta.

## Offerte, tempo e aggiudicazione

Un’offerta deve superare l’importo corrente, provenire da una squadra partecipante con membership attiva e rispettare sia i posti del ruolo sia il budget. Il budget disponibile riserva 1 credito per ogni posto ancora da completare dopo l’acquisto.

Il tempo autorevole proviene da SQL Server, letto dopo l’acquisizione del lock della stagione. Un’offerta con `now >= deadline` è tardiva. Ogni offerta accettata riavvia l’intera durata; rifiuti e replay non spostano la scadenza.

Un lavoratore API verifica le scadenze ogni 500 ms, anche senza browser collegati. Sotto lo stesso lock usato dalle offerte ricontrolla la scadenza, assegna il giocatore, addebita il budget, registra il movimento e avanza il turno nella stessa transazione. Vincoli univoci e stato impediscono doppie aggiudicazioni. Il turno salta automaticamente le squadre che hanno completato il ruolo corrente, anche se hanno ancora posti negli altri ruoli; quando sono tutte complete termina la sessione. Dopo un riavvio vengono recuperate le aste già scadute.

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

Il giocatore nello snapshot espone anche BirthDate, Nationality e PreferredFoot, letti dalla ListEntry della versione fissata nella sua asta. Lo stato include l’ultima asta anche dopo la chiusura; `currentTeamId` è null per una sessione Completed. La rosa comprende gli acquisti dell’intera stagione, inclusi quelli di sessioni precedenti, con nomi e club dal listone originale di ciascun acquisto.

### Contesto sala e catalogo

`AuctionRoom` restituisce `{leagueId, leagueSeasonId, myTeamId, canManage, sessionId, listVersionId, teams, participants}` anche prima della creazione della sessione. `myTeamId` proviene da TeamMembers con membership attiva e rimane null per chi organizza senza giocare. `canManage` richiede SuperAdmin verificato nel database oppure organizzatore attivo. Le `teams` hanno `{id, name, budget, goalkeepers, defenders, midfielders, forwards}` e comprendono le squadre della stagione con almeno un membro attivo, ordinate per nome/id. Budget e conteggi riflettono gli acquisti persistiti della stagione. `participants` contiene i membri attivi della lega con `{userId, displayName, teamName, isOrganizer}`, senza email o dettagli degli inviti; la squadra è relativa alla stagione richiesta e può essere null. La lista è disponibile anche ai partecipanti, dopo la stessa verifica di accesso alla sala.

`sessionId` indica la sessione Active/Paused, altrimenti la Completed creata più recentemente, oppure null. `listVersionId` segue il listone della sessione selezionata; in assenza di sessioni segue la configurazione stagionale, eventualmente null. Questa lettura e il catalogo richiedono appartenenza attiva o SuperAdmin ancora valido: membri Pending, estranei e vecchie claim revocate non danno accesso. Le query Dapper acquisiscono il lock condiviso della stagione e ricontrollano l’autorizzazione dopo il lock.

`Catalog` restituisce `AuctionPage` (`items`, `page`, `pageSize`, `total`) con elementi `{playerId, name, role, clubName, isAvailable, teamId}` dal listone fissato nella sessione. `availableOnly` è true per default: esclude acquisti di tutta la stagione e la chiamata aperta, anche in un’altra sessione della stessa stagione. Con false include anche questi giocatori; `teamId` identifica soltanto la squadra acquirente, ed è null per la chiamata ancora aperta. `search` cerca nome e nome completo, rimuove gli spazi esterni e accetta fino a 200 caratteri, trattando letteralmente i caratteri jolly SQL. `role` accetta P/D/C/A senza distinzione maiuscole/minuscole. Default page=1 e pageSize=30, limiti rispettivamente 1–10000 e 1–100; ordine stabile per nome/id. Filtri e paginazione non validi restituiscono 400.

## SignalR e recupero client

Hub autenticato `/hubs/Auctions`. `WatchSession(sessionId)` restituisce lo stato autorevole e registra l’osservazione; `UnwatchSession(sessionId)` la rimuove. Ogni connessione può osservare al massimo 16 sessioni.

`AuctionPresenceChanged` comunica `sessionId`, `connectedUsers` e `users` dopo la verifica dei permessi effettuata dall’observer. `users` contiene gli stessi campi pubblici di `participants` per gli osservatori autorizzati attualmente connessi, inclusi eventuali SuperAdmin senza membership. Le notifiche confrontano le identità e i profili, non solo il conteggio: ingressi/uscite simultanei e cambi di nome a parità di numero vengono propagati. Il conteggio include gli utenti distinti con almeno una sottoscrizione alla sessione, organizzatori compresi, e si aggiorna a ingresso, uscita e revoca. Più connessioni dello stesso utente non aumentano il numero. Il conteggio è in memoria e relativo alla singola istanza API: per una distribuzione multiistanza occorre aggregare le presenze in un registro condiviso. Le disconnessioni improvvise si riflettono dopo il rilevamento del timeout SignalR.

`AuctionChanged` comunica SessionId e Version. Il client ignora versioni già viste e ricarica lo stato; dopo una riconnessione ripete WatchSession e recupera le ricevute dei comandi con risposta incerta. Nessuna offerta offline e nessun timer client decide l’aggiudicazione.

Ogni istanza API osserva nel database le versioni delle proprie sottoscrizioni e ricontrolla i permessi prima della notifica. L’Hub è configurato per chiudere le connessioni alla scadenza dell’autenticazione; perdita della membership rimuove l’osservazione. Letture e comandi ricontrollano anche il ruolo SuperAdmin nel database: una vecchia claim non conserva privilegi revocati. Per questo protocollo basato su versioni SQL non è necessario un backplane SignalR.

Gli errori attesi dell’Hub contengono un codice stabile (`auth.forbidden`, `resource.not_found`, `auction.watch_limit`). Il protocollo SignalR può anteporre una descrizione del metodo e dell’eccezione; il codice rimane nel messaggio. I dettagli delle eccezioni tecniche restano nascosti.

`Cors:AllowedOrigins` contiene le origini esplicite ammesse, senza wildcard. In Development i default sono `http://localhost:6060` e `http://localhost:6061`. Un controllo Origin dedicato protegge anche l’upgrade WebSocket; CORS da solo non copre quel percorso. In produzione configurare l’origine effettiva del sito e usare HTTPS.

## Verifica locale

Si usano gli stessi Dockerfile, Compose e comandi `just` del backend: `just up-all`, API/Scalar su 6060, SQL su 14333. La migrazione aggiunge sette tabelle: AuctionSessions, CallOrderEntries, PlayerAuctions, Bids, RosterEntries, BudgetMovements e CommandReceipts.

I test d’integrazione usano SQL Server reale e dati sintetici in database temporanei. Coprono concorrenza, ricevute, timer, aggiudicazione, isolamento, HTTP e SignalR. Esiti e limiti della verifica corrente sono registrati nel [passaggio backend](../../../docs/workflow/backend-handoff.md); queste prove locali non misurano la capacità di carico su Azure SQL DTU.

### Passaggio diretto del turno

Il controllo `GoToTurn` accetta `targetTeamId` (facoltativo nel contratto, necessario per questa azione). Solo l’organizzatore può selezionare una squadra presente nell’ordine della stessa sessione, lega e stagione, con posti disponibili nel ruolo corrente; il comando è valido tra due calciatori e conserva l’ordine. Destinazione assente/non partecipante: `auction.invalid_target`; rosa completa: `auction.roster_full`; ruolo corrente completo: `auction.role_full`. Destinazione già corrente è un no-op. Il comando usa ricevuta idempotente, lock e notifica dei controlli esistenti; la destinazione entra nell’hash solo per GoToTurn, conservando la compatibilità delle ricevute dei controlli precedenti. Le prove temporanee di turno fisso sono solo un aiuto frontend Development e usano questo stesso comando.

### Stato della fase

Lo snapshot HTTP e SignalR include `currentRole` (P, D, C, A; null per sessioni concluse). Fase e chiamante sono calcolati sotto il lock stagionale condiviso. Il turno viene avanzato dopo ogni assegnazione nella stessa transazione; al cambio di ruolo si prosegue nell’ordine circolare dalla squadra successiva al chiamante. Reorder conserva il chiamante idoneo e GoToTurn non può forzare una squadra senza posti nel ruolo. Nessuna migrazione è necessaria: una sessione precedente con chiamante non più idoneo espone automaticamente la prima squadra idonea a partire da quella posizione e la successiva chiamata usa la stessa regola. Un calciatore già all’asta al momento dell’aggiornamento mantiene il proprio ruolo fino all’aggiudicazione; la chiamata successiva segue le fasi.

## Bomba: offerte segrete

Il chiamante può aprire una Bomba sul calciatore selezionato nel ruolo corrente, con `POST /api/Auctions/Sessions/{sessionId}/Bombs` (`requestId`, `playerId`). Il server registra i partecipanti idonei nell’ordine di chiamata: squadre con membro attivo, posto nel ruolo e budget che conserva la riserva per gli slot restanti. La Bomba parte nello stato `Waiting`: una lobby condivisa di 60 secondi, senza conferma dell’organizzatore. Il worker apre automaticamente `Collecting` alla scadenza e assegna altri 60 secondi completi a partire dal tempo SQL della transizione. Durante l’attesa le offerte sono rifiutate; cancellazione, riserva del calciatore e blocchi del flusso ordinario sono già attivi. Riconnessioni e replay non riavviano la scadenza. Non viene creata un’asta Classic aperta né un’offerta iniziale automatica.

Ogni squadra può usare al massimo una Bomba non annullata per sessione d’asta. L’avvio accettato consuma la disponibilità anche in assenza di offerte; l’annullamento la restituisce, in qualunque fase avvenga. Partecipare alle Bombe altrui non la consuma. Lo storico persistito vale anche per le Bombe già avviate prima dell’introduzione del limite, escludendo quelle annullate. Il controllo avviene nella transazione sotto il lock stagionale e rifiuta ulteriori avvii con `auction.bomb_already_used` (409); il replay dello stesso RequestId conserva la ricevuta originale. Lo snapshot espone `usedBombTeamIds`, indipendente da `currentBomb`, per mantenere il limite visibile dopo riconnessione o nuove chiamate. Una nuova sessione ripristina la disponibilità.

`POST .../BombBids` (`requestId`, `bombAuctionId`, `round`, `amount`) conferma una sola offerta immutabile per squadra e turno. Ogni turno dura 60 secondi SQL; le conferme mancanti alla scadenza sono escluse, senza azione Passo. Il minimo iniziale è 1 credito. La raccolta termina anticipatamente quando tutti hanno confermato. I tre comandi Bomba usano le ricevute persistite esistenti, il cui `auctionId` identifica la Bomba. Nuovi tentativi con un altro RequestId non modificano una conferma già acquisita.

Dopo la raccolta il server attende 30 secondi e pubblica una nuova offerta ogni 6 secondi, in ordine crescente, con l’ordine dei partecipanti come spareggio della sola visualizzazione. L’ultima offerta rimane visibile per 6 secondi prima dell’esito. Un massimo unico viene assegnato; a parità del massimo il server apre un altro turno di 60 secondi fra le sole squadre in parità, con minimo uguale all’importo della parità. Gli spareggi entrano direttamente in `Collecting`, senza ripetere la lobby. Ulteriori parità ripetono la stessa regola. Senza conferme l’esito è `NoSale`: il calciatore resta libero e il turno avanza. La chiusura con assegnazione scrive nella stessa transazione una PlayerAuction già Closed, la conferma vincente nello storico Bids, la rosa e il movimento di budget, poi avanza la normale fase per ruoli. Il timestamp dell’offerta storica è quello della conferma originale.

`POST .../CancelBomb` (`requestId`, `bombAuctionId`) è riservata a organizzatore attivo o SuperAdmin verificato; annulla durante attesa, raccolta o rivelazione senza assegnazione, addebiti o cambio del chiamante. Durante una Bomba attiva tutti i comandi ordinari Players/Bids/Control sono rifiutati con `auction.bomb_in_progress`; il catalogo considera il calciatore indisponibile. Alla conclusione o annullamento può partire una nuova chiamata.

Lo snapshot `currentBomb` contiene identità e immagini del calciatore, chiamante, `status` (`Waiting`, `Collecting`, `Revealing`, `Completed`, `Cancelled`, `NoSale`), `round`, `minimumAmount`, `deadline` (fine dell’attesa in Waiting, fine delle offerte in Collecting), `revealStartedAt`, `nextRevealAt`, `participants` (`teamId`, `hasSubmitted`), `revealedOffers` (`teamId`, `amount`), `ownAmount` e risultato finale (`playerAuctionId`, `winningTeamId`, `winningAmount`). I partecipanti mantengono un ordine stabile indipendente dagli importi. La query carica gli importi pubblici soltanto fino al contatore persistito di rivelazione; `ownAmount` richiede la membership attiva nella propria squadra e lega/stagione, anche per organizzatori o SuperAdmin. Il vincitore è null fino al completamento. Annullare non rende pubbliche le offerte ancora segrete. Una nuova chiamata ordinaria o Bomba sostituisce il risultato precedente nello snapshot.

Le tabelle EF `BombAuctions` e `BombOffers` conservano fasi, scadenze, partecipanti e conferme di ogni turno, con chiavi esterne che mantengono lo scope di lega e stagione. Il worker esistente invoca `AuctionEngine.AdvanceBombsAsync` ogni 500 ms anche senza client: ogni avanzamento ricontrolla stato e tempo sotto il lock esclusivo stagionale e incrementa la versione soltanto nella transazione riuscita. La ripresa dopo un’interruzione conserva sei secondi fra gli aggiornamenti successivi; non comprime tutta la rivelazione in un solo snapshot. HTTP e SignalR recuperano lo stesso stato privato autorizzato dopo la riconnessione.

### Prova manuale della Bomba con sette avversari

Il [runner locale](../../scripts/bomb-test/README.md) prepara gli altri account demo mentre Luca usa Atletico Spritz nell’interfaccia. `--check` è in sola lettura; `--watch` attende una nuova Bomba e offre nella fase Collecting. Il runner è vincolato alla lega demo e al database locale, conserva le ricevute ed esclude sempre la squadra di Luca. Non simula connessioni browser/SignalR né modifica i timer.
