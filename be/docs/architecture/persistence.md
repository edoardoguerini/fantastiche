# EF Core, Identity e Dapper

| Responsabilità | Strumento |
| --- | --- |
| Schema, mapping e migrazioni | EF Core |
| Account, manager e store | ASP.NET Core Identity + EF Core |
| Configurazione ordinaria | EF Core |
| Query delle schermate | Dapper |
| Avvio, rilanci, aggiudicazioni | Dapper con transazioni SQL |

Un solo database e schema. La tabella __EFMigrationsHistory registra le migrazioni. I nomi delle tabelle applicative sono plurali; Identity conserva la propria struttura AspNet*. Non duplicare Users.

## Operazioni critiche

La transazione acquisisce la protezione necessaria sullo stato dell’asta, verifica scadenza e budget, registra offerta/ricevuta e aggiorna scadenza/versione. La chiusura usa lo stesso meccanismo e salva assegnazione e addebito atomicamente. Strategia di lock/isolamento da definire e provare su SQL Server.

Le ricevute idempotenti permettono di recuperare un esito anche dopo perdita della conferma. Retry di errori transitori richiedono identificativi e gestione coerenti, non un secondo addebito.

Dapper non applica query filter, interceptor audit/soft-delete o tracking EF. Scope, audit e aggiornamenti vanno esplicitati nel comando SQL. Non usare DbContext obsoleti per sovrascrivere lo stato aggiornato da Dapper.

## Letture

Colonne esplicite, parametri, query key coerenti con la lega, indici su filtri/join/ordinamenti, paginazione degli storici. EF in sola lettura senza tracking quando appropriato. La scelta degli indici si misura: niente indice su ogni colonna per default.

Timestamp UTC; crediti interi; chiavi e foreign key coerenti con la [specifica database](../../../docs/superpowers/specs/2026-09-09-catalogo-e-database-proposta.md). Chiavi Guid UUIDv7 per il primo incremento; membership con chiavi composte e FK coerenti lega/stagione. L’onboarding usa transazione serializable e sp_getapplock dedicato; questa scelta non definisce il futuro lock dell’asta.
