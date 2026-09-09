# Fantastiche Backend — Regole per assistenti

Applica anche [CLAUDE.md root](../CLAUDE.md). Il backend implementa bootstrap, accesso, leghe/inviti, coda email, catalogo Classic versionato e motore d’asta con ricevute persistite, chiusura automatica e notifiche SignalR. Dettagli operativi nella [guida aste](docs/domains/auctions.md).

## Architettura

Manteniamo la nomenclatura ACKS: `Application` è l’host, `Infrastructure` ospita le feature e gli handler. Non invertirne il significato in nome di altre convenzioni.

Dipendenze consentite: Application → Infrastructure/Core; Infrastructure → Gateways/Core; Gateways → Core. Core non dipende dagli altri progetti, da EF, Identity o Dapper. Scheduler comporrà Infrastructure per il dispatcher delle email; l’host usa BackgroundService per la coda email.

- Boundary HTTP in `Application/Modules/<Feature>/`, SignalR in `Application/Hubs/`.
- Feature in `Infrastructure/<Feature>/`: `Domain/`, `Persistence/`, cartelle per use-case, `Payloads/`.
- Le configuration EF di feature stanno in `Persistence/`; quelle trasversali accanto al relativo modello in `Common/`.
- Namespace di feature flat: `Fantastiche.Infrastructure.Auctions` anche nelle sottocartelle.
- Contratti di dispatch tramite `IRequestPublisher`, come nel riferimento, implementati con risoluzione DI degli handler. Nessuna dipendenza MediatR.
- Gestione centralizzata delle versioni NuGet in `Directory.Packages.props`, proprietà comuni in `Directory.Build.props`. SDK e versione target dichiarati in global.json/Directory.Build.props.

Dettaglio: [struttura](docs/architecture/project-structure.md), [CQRS](docs/architecture/cqrs.md).

## Persistenza e Identity

- Azure SQL DTU; schema unico e migrazioni EF Core, anche per tabelle usate da Dapper.
- ASP.NET Core Identity tramite i suoi manager e store EF. Nessuna tabella utenti duplicata.
- EF Core per configurazione ordinaria; Dapper per letture e avvio/rilancio/chiusura dell’asta.
- SQL parametrizzato, liste di colonne esplicite e paginazione degli storici.
- Transazioni brevi; tutte le query del comando condividono connessione e transazione.
- Dapper non esegue interceptor né query filter EF: autorizzazione, scope, audit e vincoli vanno gestiti esplicitamente.
- Nessun soft-delete generalizzato dello storico offerte o budget. Le correzioni di dominio richiedono operazioni tracciate.
- Non fare salvataggi EF su entità tracciate prima di una modifica Dapper senza una gestione esplicita dello stato.

Dettaglio: [persistence](docs/architecture/persistence.md), [isolamento](docs/domains/league-isolation.md).

## Boundary e regole

- Moduli HTTP sottili: validano il payload, ricavano identità dal contesto e delegano all’handler.
- Stessa separazione per SignalR: validazione dei messaggi e autorizzazione, nessuna logica d’asta nell’Hub.
- Pattern ACKS: `IRegistrableModule`, `MapGroup`, `ApiResponse<T>`, helper `ApiResults`; tipi implementati nel bootstrap.
- Route HTTP sotto `/api`, segmenti risorsa in PascalCase; nome, descrizione OpenAPI e autorizzazione espliciti.
- Errori attesi con codice stabile; HTTP restituisce lo status corretto. SignalR restituisce un esito correlato alla richiesta.
- Validazione sintattica al boundary (FluentValidation come convenzione di riferimento); invarianti nell’handler, dentro la transazione quando dipendono dallo stato.
- Identità e permessi sempre sul server: i gruppi SignalR non sostituiscono l’autorizzazione.
- Logging strutturato tramite `ILogger<T>`, correlation ID e niente segreti.
- Inviti e messaggi email persistiti nella stessa transazione; invio Mailgun tramite Scheduler fuori dalla transazione. Vedi [inviti ed email](docs/domains/invitations-email.md).

Guide: [moduli](docs/architecture/module-pattern.md), [convenzioni](docs/conventions/naming.md), [feature](docs/how-to/add-a-module.md), [review](docs/quality/code-review.md).
