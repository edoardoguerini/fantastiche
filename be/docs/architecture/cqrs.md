# CQRS e dispatch

Riprendiamo la separazione command/query del riferimento. HTTP e SignalR delegano tramite un contratto `IRequestPublisher` implementato mediante DI, senza una libreria mediator nei boundary.

- `PlaceBidCommand` / `PlaceBidCommandHandler`: modifica stato.
- `GetAuctionStateQuery` / `GetAuctionStateQueryHandler`: sola lettura.
- Command/query hanno correlation ID e contesto autenticato ricavati dal server; le mutazioni critiche hanno anche request ID idempotente.
- DTO `AuctionDetails` o `PlayerListItem`; un handler non restituisce entità EF tracciate al frontend.
- Query non scrivono. Command sceglie un solo percorso EF o Dapper secondo la responsabilità.
- Nessuna dipendenza MediatR: IRequestPublisher risolve IRequestHandler tramite DI.
