using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Common;
using FluentValidation;

namespace Fantastiche.Application.Modules.Auctions;

public sealed class AuctionsModule : IRegistrableModule
{
    public void RegisterEndpoints(IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/Auctions/Sessions").WithTags("Auctions").RequireAuthorization();
        group.MapPost("", Create).WithName("CreateAuctionSession")
            .WithSummary("Crea sessione d’asta")
            .WithDescription("Organizzatore o SuperAdmin avvia una sessione con ordine di squadre e listone della stagione. Una sola sessione attiva per stagione.")
            .Produces<ApiResponse<AuctionSessionView>>(StatusCodes.Status201Created);
        group.MapGet("/{sessionId:guid}", Get).WithName("GetAuctionState")
            .WithSummary("Stato autorevole dell’asta")
            .WithDescription("Restituisce turno, prezzo, scadenza, budget e versione dello stato. Richiede appartenenza attiva alla lega o SuperAdmin.")
            .Produces<ApiResponse<AuctionSessionView>>();
        CommandMetadata(group.MapPost("/{sessionId:guid}/Players", Start).WithName("StartPlayerAuction")
            .WithSummary("Chiama giocatore")
            .WithDescription("La squadra di turno avvia il giocatore con offerta iniziale di 1 credito. RequestId permette il recupero dello stesso esito."));
        CommandMetadata(group.MapPost("/{sessionId:guid}/Bids", Bid).WithName("PlaceBid")
            .WithSummary("Offri un importo totale")
            .WithDescription("Accetta un totale assoluto maggiore del prezzo corrente entro la scadenza SQL. Il rilancio valido riavvia l’intera durata; rifiuti e replay conservano il timer."));
        CommandMetadata(group.MapPost("/{sessionId:guid}/Control", Control).WithName("ControlAuctionSession")
            .WithSummary("Gestisci la sessione")
            .WithDescription("Organizzatore o SuperAdmin: Pause, Resume, SkipTurn, Reorder o Complete. Modifiche tra giocatori; Reorder conserva le squadre partecipanti."));
        CommandMetadata(group.MapPost("/{sessionId:guid}/Bombs", StartBomb).WithName("StartBomb")
            .WithSummary("Sgancia la Bomba")
            .WithDescription("Il chiamante apre un’attesa condivisa di 60 secondi, seguita automaticamente dalla raccolta segreta di 60 secondi per le squadre idonee."));
        CommandMetadata(group.MapPost("/{sessionId:guid}/BombBids", SubmitBombOffer).WithName("SubmitBombOffer")
            .WithSummary("Conferma offerta segreta")
            .WithDescription("Una sola conferma per squadra e turno; server e ricevuta proteggono importo e scadenza."));
        CommandMetadata(group.MapPost("/{sessionId:guid}/CancelBomb", CancelBomb).WithName("CancelBomb")
            .WithSummary("Annulla la Bomba")
            .WithDescription("Organizzatore o SuperAdmin annulla una Bomba attiva senza addebiti."));
        group.MapGet("/{sessionId:guid}/Commands/{requestId:guid}", Receipt).WithName("GetAuctionReceipt")
            .WithSummary("Recupera esito del comando")
            .WithDescription("Legge la propria ricevuta persistita, anche quando il comando era stato rifiutato. Non genera una nuova offerta.")
            .Produces<ApiResponse<AuctionCommandResult>>();
        group.MapGet("/{sessionId:guid}/Players/{playerAuctionId:guid}/Bids", Bids).WithName("GetAuctionBids")
            .WithSummary("Storico offerte accettate")
            .WithDescription("Storico paginato e ordinato per sequenza server, limitato al giocatore della sessione richiesta.")
            .Produces<ApiResponse<AuctionPage<AuctionBidView>>>();
        group.MapGet("/{sessionId:guid}/Roster", Roster).WithName("GetAuctionRoster")
            .WithSummary("Rosa della stagione")
            .WithDescription("Acquisti della stagione, con nome e ruolo originali dell’aggiudicazione. Filtro facoltativo per squadra e paginazione.")
            .Produces<ApiResponse<AuctionPage<AuctionRosterView>>>();
        group.MapGet("/{sessionId:guid}/Catalog", Catalog).WithName("GetAuctionCatalog")
            .WithSummary("Listone della sessione con disponibilità stagionale")
            .WithDescription("Ricerca e ruolo paginati. Per default esclude acquisti della stagione e giocatore chiamato; availableOnly=false include anche gli indisponibili e la squadra acquirente.")
            .Produces<ApiResponse<AuctionPage<AuctionCatalogPlayerView>>>();
    }

    private static void CommandMetadata(RouteHandlerBuilder route)
    {
        foreach (var status in new[] { 200, 400, 403, 404, 409 })
            route.Produces<ApiResponse<AuctionCommandResult>>(status);
    }

    private static IResult CommandResponse(AuctionCommandResult result)
        => Results.Json(new ApiResponse<AuctionCommandResult>(result.Accepted, result,
            result.Accepted ? [] : [new ApiError(result.ErrorCode ?? "auction.rejected", result.Message ?? "Comando rifiutato.")]),
            statusCode: result.StatusCode);

    private static async Task<IResult> Create(CreateAuctionSessionRequest request, HttpContext context,
        IValidator<CreateAuctionSessionRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var result = await publisher.SendAsync<CreateAuctionSessionCommand, AuctionSessionView>(
            new(context.CreateRequestContext(), request.LeagueId, request.LeagueSeasonId, request.TeamOrder), ct);
        return ApiResults.Created($"/api/Auctions/Sessions/{result.Id}", result);
    }

    private static async Task<IResult> Get(Guid sessionId, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
        => ApiResults.Ok(await publisher.QueryAsync<GetAuctionStateQuery, AuctionSessionView>(new(context.CreateRequestContext(), sessionId), ct));

    private static async Task<IResult> Start(Guid sessionId, StartPlayerAuctionRequest request, HttpContext context,
        IValidator<StartPlayerAuctionRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResponse(await publisher.SendAsync<StartPlayerAuctionCommand, AuctionCommandResult>(
            new(context.CreateRequestContext(), sessionId, request.RequestId, request.PlayerId, request.DurationSeconds, request.Increments), ct));
    }

    private static async Task<IResult> Bid(Guid sessionId, PlaceBidRequest request, HttpContext context,
        IValidator<PlaceBidRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResponse(await publisher.SendAsync<PlaceBidCommand, AuctionCommandResult>(
            new(context.CreateRequestContext(), sessionId, request.RequestId, request.PlayerAuctionId, request.Amount), ct));
    }

    private static async Task<IResult> Control(Guid sessionId, ControlAuctionSessionRequest request, HttpContext context,
        IValidator<ControlAuctionSessionRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResponse(await publisher.SendAsync<ControlAuctionSessionCommand, AuctionCommandResult>(
            new(context.CreateRequestContext(), sessionId, request.RequestId, request.Action, request.TeamOrder, request.TargetTeamId), ct));
    }

    private static async Task<IResult> StartBomb(Guid sessionId, StartBombRequest request, HttpContext context,
        IValidator<StartBombRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResponse(await publisher.SendAsync<StartBombCommand, AuctionCommandResult>(
            new(context.CreateRequestContext(), sessionId, request.RequestId, request.PlayerId), ct));
    }

    private static async Task<IResult> SubmitBombOffer(Guid sessionId, SubmitBombOfferRequest request, HttpContext context,
        IValidator<SubmitBombOfferRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResponse(await publisher.SendAsync<SubmitBombOfferCommand, AuctionCommandResult>(
            new(context.CreateRequestContext(), sessionId, request.RequestId, request.BombAuctionId, request.Round, request.Amount), ct));
    }

    private static async Task<IResult> CancelBomb(Guid sessionId, CancelBombRequest request, HttpContext context,
        IValidator<CancelBombRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return CommandResponse(await publisher.SendAsync<CancelBombCommand, AuctionCommandResult>(
            new(context.CreateRequestContext(), sessionId, request.RequestId, request.BombAuctionId), ct));
    }

    private static async Task<IResult> Receipt(Guid sessionId, Guid requestId, HttpContext context, IRequestPublisher publisher, CancellationToken ct)
        => ApiResults.Ok(await publisher.QueryAsync<GetAuctionReceiptQuery, AuctionCommandResult>(new(context.CreateRequestContext(), sessionId, requestId), ct));

    private static async Task<IResult> Bids(Guid sessionId, Guid playerAuctionId, int? page, int? pageSize, HttpContext context,
        IValidator<AuctionPageRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        var request = new AuctionPageRequest(page ?? 1, pageSize ?? 50);
        await validator.ValidateAndThrowAsync(request, ct);
        return ApiResults.Ok(await publisher.QueryAsync<GetAuctionBidsQuery, AuctionPage<AuctionBidView>>(
            new(context.CreateRequestContext(), sessionId, playerAuctionId, request.Page, request.PageSize), ct));
    }

    private static async Task<IResult> Catalog(Guid sessionId, string? search, string? role, bool? availableOnly, int? page, int? pageSize,
        HttpContext context, IValidator<AuctionCatalogRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        var request = new AuctionCatalogRequest(search?.Trim(), role?.Trim().ToUpperInvariant(), page ?? 1, pageSize ?? 30);
        await validator.ValidateAndThrowAsync(request, ct);
        return ApiResults.Ok(await publisher.QueryAsync<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(
            new(context.CreateRequestContext(), sessionId, request.Search, request.Role, availableOnly ?? true, request.Page, request.PageSize), ct));
    }

    private static async Task<IResult> Roster(Guid sessionId, Guid? teamId, int? page, int? pageSize, HttpContext context,
        IValidator<AuctionPageRequest> validator, IRequestPublisher publisher, CancellationToken ct)
    {
        var request = new AuctionPageRequest(page ?? 1, pageSize ?? 50);
        await validator.ValidateAndThrowAsync(request, ct);
        return ApiResults.Ok(await publisher.QueryAsync<GetAuctionRosterQuery, AuctionPage<AuctionRosterView>>(
            new(context.CreateRequestContext(), sessionId, teamId, request.Page, request.PageSize), ct));
    }
}
