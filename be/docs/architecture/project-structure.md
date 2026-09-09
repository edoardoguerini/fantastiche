# Struttura backend

Solution e progetti sono implementati nel primo incremento. Catalogo e asta restano cartelle preparatorie.

```text
be/
  src/
    Fantastiche.Core/
      Auth/ Common/ Configurations/ Exceptions/ Leagues/ Storage/
    Fantastiche.Gateways/
      Mailgun/ AzureBlob/ Fantacalcio/
    Fantastiche.Infrastructure/
      Common/
        Authentication/ Authorization/ Persistence/
      Emails/
      Leagues/
      Catalog/
      Teams/
      Auctions/
    Fantastiche.Application/
      Modules/
      Hubs/
      Infrastructure/
    Fantastiche.Scheduler/       # host BackgroundService della coda email
  tests/
    Fantastiche.UnitTests/
    Fantastiche.IntegrationTests/
  docs/
```

Ogni feature Infrastructure segue il riferimento:

```text
Auctions/
  Domain/
  Persistence/
  Payloads/
  StartPlayerAuction/
  PlaceBid/
  ClosePlayerAuction/
  GetAuctionState/
```

- Domain: modello ed enum della feature.
- Persistence: configuration EF, query SQL e componenti Dapper della feature.
- Use-case: Command/Query e Handler; SQL semplice può rimanere nell’handler, SQL complesso sta in Persistence.
- Payloads: DTO condivisi tra use-case.
- Common/Persistence: DbContext, apertura connessioni e migrations, senza repository generico obbligatorio sopra Dapper.
- Namespace flat per feature: `Fantastiche.Infrastructure.Auctions`.
- Application/Modules: modulo HTTP e Payloads con Request/Validator.
- Application/Hubs: trasporto SignalR che delega agli stessi casi d’uso.
- Scheduler: invia la coda email; non presumere che un cron sia adatto alla precisione del timer d’asta.

Solution e configurazione centralizzata usano i nomi `Fantastiche.slnx`, `Directory.Build.props` e `Directory.Packages.props`.
