# Piano motore d’asta

> Eseguire con superpowers:subagent-driven-development; branch attuale autorizzato, nessuna nuova richiesta di conferma per passaggi locali reversibili.

**Goal:** ciclo completo d’asta Classic server-authoritative con recupero e notifiche.
**Architecture:** schema EF, comandi/query Dapper, HTTP/SignalR sottili e lavoratori Application.
**Tech Stack:** .NET10, SQL Server, Dapper, Identity, SignalR built-in, xUnit.
**Spec:** [motore-asta-design](../specs/2026-09-09-motore-asta-design.md).

## Vincoli globali

ACKSD naming/struttura, namespace flat, EF solo schema e Dapper unico scrittore aste. Nessun provider esterno o frontend. Port6060, Docker/just esistenti. Tempo SQL, lock stagione, ricevute durabili, autorizzazione in ogni percorso privato. Niente commit/push automatici degli altri file preparatori.

## Task1 — Schema, contratti e query (agente schema)

Files: Infrastructure/Auctions/{Domain,Persistence/*Configuration.cs,Payloads,GetAuctionState,GetActiveAuction,GetAuctionReceipt,GetAuctionBids,GetAuctionRoster}; command record nei folder CreateAuctionSession,StartPlayerAuction,PlaceBid,ControlAuctionSession. DbContext e nuova migration. Test: IntegrationTests/Auctions/AuctionQueryTests.cs.
Interfacce esatte nello spec; nessuna modifica motore o helper AuctionSqlLock (agente motore). Letture su lock Shared, autorizzazione Active/SuperAdmin, proiezioni Dapper paginate.
- [x] Scrivere schema/contratti e test significativi prima dei query handler; osservare errore funzionale.
- [x] Implementare query e migrazione, verificare SQL dedicato.
- [x] Review schema/contratti/query e allineamento migration.

## Task2 — Motore e concorrenza (agente motore)

Files: Infrastructure/Auctions/AuctionEngine*.cs (partial per responsabilità), Persistence/AuctionSqlLock.cs e command handler; Infrastructure/DependencyInjection.cs; IntegrationTests/Auctions/AuctionEngineTests.cs e fixture/helper della stessa feature.
Consuma tipi/schema task1; espone metodi esatti nello spec e restituisce risultati/ricevute persistiti.
- [x] Test SQL rossi per ciclo base e rifiuti, poi implementazione Dapper atomica.
- [x] Concorrenza/replay/budget/ruoli/turni e race timer con test prima delle relative correzioni.
- [x] Completare controlli organizzatore e recupero delle scadenze, test dedicati verdi.
- [x] Review indipendente delle transazioni e ricevute.

## Task3 — SignalR e lavoratori (agente realtime)

Files: Application/Hubs, Application/Infrastructure/Auctions, eventuale Infrastructure/Auctions/ObserveAuctions per query batch osservatori. IntegrationTests/Auctions/AuctionRealtimeTests.cs. Nessuna modifica Program.cs: fornire metodi extension AddAuctionRealtime/MapAuctionRealtime o istruzioni esatte per root.
Consuma engine CloseExpiredAsync e GetAuctionStateQuery; registra i worker, registry connessioni e hub read-only. Default500ms; recheck permessi DB, Origin/CORS explicit, scadenza auth, no nuove dipendenze necessarie per il protocollo JSON dei test WebSocket.
- [x] Test membership/registrazione/notifica e worker con SQL reale.
- [x] Implementare hub, observer e closer; verificare Origin e rimozione accesso.
- [x] Eseguire test e review boundaries.

## Task4 — HTTP e integrazione (coordinatore)

Files: Application/Modules/Auctions e Modules/Leagues/LeagueAuctionsModule.cs; Program.cs integra realtime; IntegrationTests/Http/AuctionHttpTests.cs partial helper esistenti; be/docs/domains/auctions.md e handoff.
- [x] Test HTTP del ciclo completo prima degli endpoint, 404 atteso.
- [x] Moduli, validator e metadata Scalar/OpenAPI, receipt sui rifiuti.
- [x] Fullsuite, fmt/lint/migrationcheck, applicazione migration locale, Docker dev/runtime, smoke6060.
- [x] Review finale e documentazione con limiti reali.
