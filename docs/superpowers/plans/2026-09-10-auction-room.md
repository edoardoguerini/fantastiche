# Sala d’asta — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Sala d’asta realmente utilizzabile da otto account demo, con aggiornamenti concorrenti e layout mobile approvato.

**Architecture:** Feature auctions separata in frontend; backend esteso solo con letture di contesto/catalogo. Motore delle offerte invariato, con SignalR per invalidare query e ricevute per recuperare gli esiti incerti.

**Tech Stack:** React, TanStack Query/Router/Form, Zod, SignalR JS, .NET 10, Dapper/SQL Server.

**Spec:** `docs/superpowers/specs/2026-09-10-auction-room-design.md`

## Global Constraints

Solo dark mode; Forest Green/crema; Sora/Geist; icone Font Awesome Light locali; linee minimali. Mutazioni con antiforgery, RequestId stabile per chiamata/offerta/controllo. Accesso verificato server, niente offerta offline o aggiudicazione client. Preservare il lavoro locale sul branch feature/fe-bootstrap; nessun commit/push richiesto.

## Task 1 — Letture backend per la sala

File: nuovi handler in `be/src/Fantastiche.Infrastructure/Auctions/`, payload e moduli HTTP, test in `be/tests/Fantastiche.IntegrationTests/Auctions/` e `Http/`.

- [x] Test SQL/HTTP: estraneo e membro Pending non leggono, SuperAdmin revocato non conserva accesso; squadra personale distinta da organizzatore; calciatore acquistato escluso dai disponibili; filtri/paginazione.
- [x] GET `/api/Leagues/{leagueId}/Seasons/{leagueSeasonId}/AuctionRoom`: `{leagueId, leagueSeasonId, myTeamId, canManage, sessionId, listVersionId, teams}`. SessionId indica sessione attiva oppure ultima completata; squadre attive della stagione con conteggi/budget.
- [x] GET `/api/Auctions/Sessions/{sessionId}/Catalog?search=&role=&page=1&pageSize=30`: `AuctionPage` con `{playerId,name,role,clubName,isAvailable,teamId}`; disponibili di default, parametro `availableOnly=true`, esclusione acquisti della stagione e chiamata aperta.
- [x] Verificare test backend e lint, documentare i contratti; nessuna modifica alle regole del motore.

## Task 2 — Client e sala frontend

File: `fe/src/features/auctions/{types,actions,hooks,components,validations}`, route `/leghe/$leagueId/asta`, link nel dettaglio lega, CSS dedicato.

- [x] Test funzioni per riserva budget, incremento totale e timer; test recupero ricevuta senza duplicare comando.
- [x] Contratti Zod e query key `['auctions', userId, ...]`, fetch autenticato con AbortSignal, paginazione listone/rosa/storico.
- [x] SignalR Watch/Unwatch e riconnessione, refresh ordinato per versione, polling di sicurezza e gestione perdita sessione.
- [x] Mutazioni con antiforgery e registro per RequestId in sessionStorage (solo payload non sensibili), ricevuta per esito incerto e nuovo invio solo esplicito con stesso payload.
- [x] Componenti tabellone squadre, asta/timer, controlli offerta e chiamata, preparazione sessione, tab Listone/Rosa/Storico, controlli organizzatore. Desktop e mobile 320px senza overflow pagina, tabellone scrollabile.

## Task 3 — Dati, integrazione e verifica

- [x] Usare API locali per importare/pubblicare/selezionare listone sintetico, creare sessione con gli otto team esistenti. Non cambiare password né attivazioni già completate.
- [x] Browser test: turni, rilanci, loading, rifiuto server, recupero esito incerto, sessione scaduta, controlli organizzatore, responsive/tastiera.
- [x] Verifica reale con due account su contesti browser separati: chiamata, rilancio concorrente, aggiornamento condiviso, chiusura server e budget/rosa.
- [x] Build, typecheck, lint, format, test pertinenti, review finale, docs e accesso diretto all’asta demo.
