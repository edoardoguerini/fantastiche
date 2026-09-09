# Motore d’asta backend

Incremento autorizzato dal proseguimento del backend, sul branch attuale `feature/be-bootstrap`. Attua la [specifica concordata](2026-09-09-asta-design.md). Struttura ACKSD: moduli/Hub in Application, feature Auctions in Infrastructure, EF per schema e Dapper per tutte le scritture e letture d’asta. Nessun frontend o provisioning Azure.

## Regole operative

Una sessione appartiene a lega/stagione e congela il listone pubblicato già scelto. L’organizzatore attivo o SuperAdmin crea la sessione con l’ordine esplicito di 1–32 squadre della stagione, ciascuna con almeno un membro attivo. Una sola sessione Active/Paused per stagione. Stato iniziale Active; versione iniziale 1. Il primo chiamante è la prima squadra nell’ordine con rosa non completa; tutte complete rifiuta la creazione con 409 `auction.rosters_complete`. I partecipanti sono congelati per la sessione. L’ordine è per squadra: qualunque suo membro attivo può operare, sempre con identità dal server. Organizzare non concede il diritto di offrire.

Solo la squadra di turno avvia un giocatore del listone non già acquistato nella stagione. Avvio con offerta iniziale 1; durata 5/10/15/20/25/30 secondi, da 1 a 10 incrementi distinti positivi e valore massimo 1.000.000. Incrementi conservati nell’ordine scelto; il server riceve sempre il totale assoluto dell’offerta. Nome/ruolo/club vengono dal listone immutabile.

Offerta valida: sessione Active, asta del giocatore Open e non scaduta, squadra partecipante e membership attiva, importo strettamente maggiore del corrente, posto disponibile nel ruolo e nella rosa, budget sufficiente anche per completare i posti residui a 1 credito. Il chiamante e il miglior offerente possono rilanciare. La nuova scadenza è l’istante SQL di accettazione più l’intera durata; rifiuti/replay non modificano timer o versione.

Il tempo autorevole è `TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')`, letto dopo il lock. Un’offerta è tardiva quando `now >= deadline`. Chiusura e offerta acquisiscono lo stesso lock SQL per stagione; la chiusura ricontrolla stato/scadenza sotto lock. Vincitore, addebito, rosa, movimento budget e avanzamento turno sono nella stessa transazione. I turni saltano le squadre con rosa completa; tutte complete chiude automaticamente la sessione. Se il lavoratore non ha ancora concluso un’asta scaduta, un nuovo avvio attende la chiusura (conflitto `auction.player_in_progress`).

Controlli organizzatore tra giocatori: Pause, Resume, SkipTurn, Reorder, Complete. Pause/Skip/Reorder/Complete rifiutati mentre un giocatore è Open. Reorder conserva esattamente le squadre e mantiene come prossimo chiamante la stessa squadra. Skip avanza circolarmente saltando rose complete. Complete consente anche chiusura anticipata tra giocatori. Un nuovo tentativo di impostare uno stato già raggiunto non incrementa la versione.

## Ricevute e transazioni

Start, Bid e Control richiedono RequestId UUID non vuoto. Ricevuta univoca per sessione/utente/requestId, con nome comando, SHA-256 del payload canonico e risultato JSON. Esiti accettati e rifiuti di dominio vengono salvati; autorizzazione alla lega e sintassi minima precedono le ricevute. Il riuso della chiave con payload o tipo comando diverso restituisce 409 senza sovrascrivere la ricevuta originale. Un replay restituisce il risultato originario, anche se l’asta è poi proseguita.

CreateSession non richiede ricevuta: il vincolo di una sessione attiva impedisce doppioni, e GET dell’attiva consente il recupero se la risposta va persa. La chiusura del lavoratore è idempotente tramite stato e vincoli univoci, senza un endpoint pubblico che consenta aggiudicazioni arbitrarie.

Connessione e transazione Dapper esplicite, isolation ReadCommitted; lock applicativo condiviso da tutti gli scrittori della stagione `Fantastiche:Auction:{leagueSeasonId:D}` con `sp_getapplock` Exclusive, owner Transaction, timeout 10000, errore SQL 51000 già mappato a 409. Le letture dello stato completo usano lo stesso lock Shared per ottenere uno snapshot coerente di più result set. L’autorizzazione viene ricontrollata nella transazione. Nessun salvataggio EF dello stato d’asta e nessun lock in memoria per correttezza.

## Schema esatto

Tutti gli ID applicativi sono Guid UUIDv7; date DateTimeOffset UTC. Status sessione: Active=0, Paused=1, Completed=2. Status giocatore: Open=0, Closed=1. Namespace flat `Fantastiche.Infrastructure.Auctions`. FK Restrict; chiavi duplicate lega/stagione coerenti tramite FK composte.

- AuctionSession: Id, LeagueId, LeagueSeasonId, ListVersionId, Status, CurrentPosition (int), Version (long), CreatedAt, CreatedByUserId. Tabelle AuctionSessions; AK (Id,LeagueSeasonId,LeagueId), AK (Id,LeagueSeasonId,LeagueId,ListVersionId); unique LeagueSeasonId filtrato Status<2; FK LeagueSeason (LeagueSeasonId,LeagueId), FK ListVersion e creatore.
- CallOrderEntry: SessionId, TeamId, LeagueSeasonId, LeagueId, Position. PK (SessionId,TeamId); unique (SessionId,Position); FK sessione tripla e Team (TeamId,LeagueSeasonId,LeagueId), usando la AK esistente Teams.
- PlayerAuction: Id, SessionId, LeagueSeasonId, LeagueId, ListVersionId, PlayerId, Number (progressivo nella sessione), CallerTeamId, WinningTeamId, Role, DurationSeconds, IncrementOptionsJson (max200), CurrentAmount, BidSequence (int), Deadline, Status, StartedAt, ClosedAt nullable. AK (Id,LeagueSeasonId,LeagueId); unique (SessionId,Number); unique SessionId filtrato Status=0; FK sessione quadrupla, ListEntry(ListVersionId,PlayerId), Caller/WinningTeam triple. Check durata, importo positivo, stato e ruolo Classic.
- Bid: Id, PlayerAuctionId, LeagueSeasonId, LeagueId, TeamId, UserId, Amount, Sequence, AcceptedAt. Unique (PlayerAuctionId,Sequence); FK asta tripla, Team tripla, User; Amount>0.
- RosterEntry: LeagueSeasonId, PlayerId, LeagueId, TeamId, PlayerAuctionId, Role, Price, AcquiredAt. PK(LeagueSeasonId,PlayerId), unique PlayerAuctionId; FK asta tripla, Team tripla e Player; Price>0, ruolo Classic.
- BudgetMovement: Id, LeagueSeasonId, LeagueId, TeamId, PlayerAuctionId, Amount (negativo), CreatedAt. Unique PlayerAuctionId; FK asta/Team triple; Amount<0.
- CommandReceipt: SessionId, UserId, RequestId, CommandType(max32), PayloadHash(nchar64), ResultJson(nvarchar4000), CreatedAt. PK(SessionId,UserId,RequestId); FK Session e User.

## Contratti condivisi

Tutti i command/query implementano IRequest<T>; handler per use case. `AuctionEngine` scoped espone CreateSessionAsync, StartAsync, BidAsync, ControlAsync, CloseAsync(Guid playerAuctionId, CancellationToken), CloseExpiredAsync(CancellationToken) -> Task<int>. Le query restano in handler dedicati. Helper comune `AuctionSqlLock.AcquireAsync(DbConnection, DbTransaction, Guid leagueSeasonId, bool exclusive, CancellationToken)`; file Persistence/AuctionSqlLock.cs di proprietà dell’implementatore motore.

Payload:
- AuctionCommandResult(Guid RequestId, Guid SessionId, Guid? AuctionId, long Version, DateTimeOffset ServerTime, bool Accepted, int StatusCode=200, string? ErrorCode=null, string? Message=null).
- AuctionSessionView(Guid Id, Guid LeagueId, Guid LeagueSeasonId, Guid ListVersionId, string Status, long Version, Guid? CurrentTeamId, IReadOnlyList<Guid> TeamOrder, AuctionPlayerView? CurrentAuction, IReadOnlyList<AuctionTeamView> Teams, DateTimeOffset ServerTime).
- AuctionPlayerView(Guid Id, Guid PlayerId, string Name, string Role, string ClubName, Guid CallerTeamId, Guid WinningTeamId, int CurrentAmount, int DurationSeconds, IReadOnlyList<int> Increments, DateTimeOffset Deadline, string Status, DateTimeOffset StartedAt, DateTimeOffset? ClosedAt).
- AuctionTeamView(Guid Id, string Name, int Budget, int Goalkeepers, int Defenders, int Midfielders, int Forwards) con conteggi acquistati, non limiti. Limiti già noti dal dettaglio lega.
- AuctionPage<T>(IReadOnlyList<T> Items,int Page,int PageSize,int Total).
- AuctionBidView(Guid Id, Guid PlayerAuctionId, Guid TeamId, Guid UserId, int Amount, int Sequence, DateTimeOffset AcceptedAt).
- AuctionRosterView(Guid PlayerId, Guid TeamId, Guid PlayerAuctionId, string Name, string Role, string ClubName, int Price, DateTimeOffset AcquiredAt).

Command:
- CreateAuctionSessionCommand(RequestContext Context,Guid LeagueId,Guid LeagueSeasonId,IReadOnlyList<Guid> TeamOrder) -> AuctionSessionView.
- StartPlayerAuctionCommand(RequestContext Context,Guid SessionId,Guid RequestId,Guid PlayerId,int DurationSeconds,IReadOnlyList<int> Increments) -> AuctionCommandResult.
- PlaceBidCommand(RequestContext Context,Guid SessionId,Guid RequestId,Guid PlayerAuctionId,int Amount) -> AuctionCommandResult.
- ControlAuctionSessionCommand(RequestContext Context,Guid SessionId,Guid RequestId,string Action,IReadOnlyList<Guid>? TeamOrder=null) -> AuctionCommandResult.

Query:
- GetAuctionStateQuery(RequestContext Context,Guid SessionId) -> AuctionSessionView.
- GetActiveAuctionQuery(RequestContext Context,Guid LeagueId,Guid LeagueSeasonId) -> AuctionSessionView?.
- GetAuctionReceiptQuery(RequestContext Context,Guid SessionId,Guid RequestId) -> AuctionCommandResult (solo ricevute del proprio utente).
- GetAuctionBidsQuery(RequestContext Context,Guid SessionId,Guid PlayerAuctionId,int Page=1,int PageSize=50) -> AuctionPage<AuctionBidView>.
- GetAuctionRosterQuery(RequestContext Context,Guid SessionId,Guid? TeamId=null,int Page=1,int PageSize=50) -> AuctionPage<AuctionRosterView>.

Letture per membri Active o SuperAdmin; mai Pending. Pagine 1..10000, dimensioni1..100. Snapshot mostra l’asta più recente per Number anche se già Closed, così il vincitore rimane visibile tra giocatori. CurrentTeamId null se Completed. Rosa e storico sempre filtrati per stagione/sessione; il catalogo identifica nome/club dallo snapshot originale dell’acquisto.

## HTTP, SignalR e recupero

POST /api/Auctions/Sessions; GET /api/Auctions/Sessions/{sessionId}; GET /api/Leagues/{leagueId}/Seasons/{leagueSeasonId}/Auction; POST /api/Auctions/Sessions/{sessionId}/Players; POST /api/Auctions/Sessions/{sessionId}/Bids; POST /api/Auctions/Sessions/{sessionId}/Control; GET /api/Auctions/Sessions/{sessionId}/Commands/{requestId}; GET /api/Auctions/Sessions/{sessionId}/Players/{playerAuctionId}/Bids; GET /api/Auctions/Sessions/{sessionId}/Roster. Identità cookie, anti-CSRF sulle mutazioni come prima; i risultati di comando includono il risultato persistito anche in caso di rifiuto, con isSuccess=false e status HTTP appropriato.

Hub read-only `/hubs/Auctions`: WatchSession(sessionId) autorizza e restituisce snapshot autorevole; UnwatchSession(sessionId). Mutazioni solo HTTP nello stesso motore. Notifica `AuctionChanged` con SessionId e Version; il client scarta versioni vecchie e ricarica lo stato. Sottoscrivere prima della lettura evita la finestra tra snapshot e iscrizione. Massimo16 sessioni per connessione, cleanup alla disconnessione.

Ogni istanza API controlla le versioni SQL dei propri osservatori ogni500ms, dopo i commit, senza richiedere un backplane. Permessi rivalutati prima delle notifiche; rimozione della sottoscrizione se l’accesso viene meno. Nessun gruppo SignalR considerato prova di autorizzazione. Chiusura connessione alla scadenza dell’autenticazione. Origin WebSocket verificata contro origini esplicite (`http://localhost:6060`/6061 in Development), CORS credenziale limitato alle origini configurate, nessun wildcard.

Un BackgroundService Application ogni500ms richiama CloseExpiredAsync, lotto50, anche al riavvio: nessun timer solo in memoria, nessuna dipendenza dal browser o dalle connessioni attive. Errori di una chiusura non devono impedire di esaminare le altre. Il Database resta autorevole durante un’interruzione delle notifiche.

## Verifiche

SQL Server reale: due servizi/connessioni concorrenti, offerte uguali/diverse, replay accettato/rifiutato, payload diverso, budget con riserva e ruoli pieni, timeout/late bids, vecchio timer dopo estensione, chiusure concorrenti, un solo addebito/rosa/movimento, advance e giro, autoComplete, scope e Pending, solo chiamante, controlli organizzatore tra giocatori. HTTP e SignalR: login/CSRF, creazione/chiamata/offerta/stato/ricevuta/rosa/storico, notifica dopo commit, Origin negata, revoca osservazione e recupero worker. Test sintetici, nessun dato CSV reale in Git. Suite83 precedente deve restare verde; migration/lint/Docker/Scalar6060 verificati.
