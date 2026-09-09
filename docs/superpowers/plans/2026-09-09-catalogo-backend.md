# Piano catalogo backend

> Esecuzione con superpowers:subagent-driven-development; continuare sul branch attuale come richiesto.

**Goal:** importare un listone Classic in bozza, pubblicarlo e selezionarlo per lega.
**Architecture:** feature Catalog e handler in Infrastructure, modulo HTTP sottile, schema EF e letture Dapper.
**Tech Stack:** .NET 10, SQL Server, EF Core, Dapper, xUnit; parser CSV della libreria standard.
**Spec:** [catalogo-backend-design](../specs/2026-09-09-catalogo-backend-design.md).

## Vincoli globali

Namespace flat Fantastiche.Infrastructure.Catalog; risorse PascalCase, documenti italiani. Nessun dato CSV reale in Git, nessuna modifica ACKSD o nuove risorse esterne. .NET 10, dipendenze già presenti. Port API 6060.

## Task 1 — Parser indipendente

File: Infrastructure/Catalog/ImportCatalog/{FantacalcioCsvParser,CatalogImportRow}.cs; UnitTests/Catalog/FantacalcioCsvParserTests.cs.
Interfaccia: `FantacalcioCsvParser.Parse(string csv)` → `IReadOnlyList<CatalogImportRow>`; record ExternalId, Name, FullName, Role, ClubName, BirthDate (DateTime), Nationality, PreferredFoot.
- [x] Scrivere test con righe sintetiche, eseguirli in rosso.
- [x] Implementare parser e limiti, validare tutto prima di restituire righe.
- [x] Eseguire test verdi e review.

## Task 2 — Persistenza e handler

File: Infrastructure/Catalog/{Domain,Persistence,Payloads,ImportCatalog,PublishCatalog,GetCatalogVersions,GetCatalogEntries,SetLeagueCatalog,GetLeagueCatalog}; DbContext, LeagueSeason e mapping; IntegrationTests/Catalog/CatalogTests.cs; nuova migrazione EF.
Contratti: ImportCatalogCommand(Context,SeasonName,Csv), PublishCatalogCommand(Context,ListVersionId); GetCatalogVersionsQuery(Context,SeasonName?,Page=1,PageSize=50), GetCatalogEntriesQuery(Context,ListVersionId,Search?,Role?,Club?,Page=1,PageSize=50); SetLeagueCatalogCommand(Context,LeagueId,LeagueSeasonId,ListVersionId), GetLeagueCatalogQuery(Context,LeagueId,LeagueSeasonId).
Payload: CatalogPage<T>(Items,Page,PageSize,Total), ListVersionView(Id,SeasonName,Status,Source,ContentHash,EntryCount,CreatedAt,PublishedAt), CatalogEntryView(PlayerId,ExternalId,Name,FullName,Role,ClubName,BirthDate,Nationality,PreferredFoot), LeagueCatalogView(LeagueId,LeagueSeasonId,ListVersionId).
- [x] Test SQL per flusso, isolamento, snapshot, replay concorrente e limiti query.
- [x] Schema e handler con transazioni atomiche, applock dedicato e autorizzazione esplicita.
- [x] Migration EF; esecuzione test SQL su DB dedicato.
- [x] Review di invarianti, query e schema.

## Task 3 — Boundary e verifica complessiva

File: Application/Modules/Catalog/{CatalogModule,Payloads/CatalogRequests}.cs; IntegrationTests/Http/HttpFlowTests.cs (stessi helper e fixture); Application/Modules/Leagues/LeagueCatalogModule.cs e Payloads/SetLeagueCatalogRequest.cs; be/docs/domains/catalog.md; docs/workflow/backend-handoff.md.
- [x] Test HTTP prima dell’implementazione: import riceve 201, pubblicazione 200, query pagina e selezione lega; 401 anonimo, 400 CSRF, bozze negate a non-admin.
- [x] Modulo sottile e validator FluentValidation, envelope e metadata OpenAPI.
- [x] Suite completa e format; aggiornamento DB locale con just, avvio container e verifica Scalar/health.
- [x] Review finale e documentazione aggiornata con esiti e limiti.

## Esiti

Completato sul branch attuale, senza push o merge. Parser: rosso iniziale 39 casi, rosso aggiuntivo ID oltre 32 caratteri, verde finale 41 casi; CSV originale verificato in sola lettura con 594 righe. SQL: 7 casi verdi, incluse regressioni stagione e lock. HTTP: primi 2 test rossi per assenza endpoint; suite finale 3 casi nuovi verdi (incluse validazioni di null e limiti). Suite completa: 36 integrazione e 47 unitari, 83/83, zero ignorati.

`just be fmt`, `just be lint`, `just be migrate-check` e `just be migrate` riusciti. `just up-all` riuscito; API 6060, SQL 14333 e Scheduler attivi. Health live/ready e Scalar HTTP 200; OpenAPI espone sei operazioni catalogo; consultazione anonima restituisce 401. Review indipendente conclusa senza rilievi aperti.

Immagini runtime API e Scheduler ricostruite localmente con successo.
