using FluentValidation;

namespace Fantastiche.Application.Modules.Auctions;

public sealed record CreateAuctionSessionRequest(Guid LeagueId, Guid LeagueSeasonId, IReadOnlyList<Guid> TeamOrder);
public sealed class CreateAuctionSessionRequestValidator : AbstractValidator<CreateAuctionSessionRequest>
{
    public CreateAuctionSessionRequestValidator()
    {
        RuleFor(x => x.LeagueId).NotEmpty();
        RuleFor(x => x.LeagueSeasonId).NotEmpty();
        RuleFor(x => x.TeamOrder).NotNull().Must(order => order is { Count: >= 1 and <= 32 })
            .WithMessage("Indica da 1 a 32 squadre nell’ordine di chiamata.");
        RuleForEach(x => x.TeamOrder).NotEmpty();
    }
}

public sealed record StartPlayerAuctionRequest(Guid RequestId, Guid PlayerId, int DurationSeconds, IReadOnlyList<int> Increments);
public sealed class StartPlayerAuctionRequestValidator : AbstractValidator<StartPlayerAuctionRequest>
{
    public StartPlayerAuctionRequestValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.PlayerId).NotEmpty();
        RuleFor(x => x.Increments).NotNull().Must(values => values is { Count: <= 10 })
            .WithMessage("Sono consentiti al massimo 10 pulsanti di incremento.");
    }
}

public sealed record PlaceBidRequest(Guid RequestId, Guid PlayerAuctionId, int Amount);
public sealed class PlaceBidRequestValidator : AbstractValidator<PlaceBidRequest>
{
    public PlaceBidRequestValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.PlayerAuctionId).NotEmpty();
        // La validità dell’importo viene valutata dal motore e conservata nella ricevuta.
    }
}

public sealed record ControlAuctionSessionRequest(Guid RequestId, string Action, IReadOnlyList<Guid>? TeamOrder = null);
public sealed class ControlAuctionSessionRequestValidator : AbstractValidator<ControlAuctionSessionRequest>
{
    public ControlAuctionSessionRequestValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();
        RuleFor(x => x.Action).NotEmpty().MaximumLength(30);
        RuleFor(x => x.TeamOrder).Must(order => order is null || order.Count <= 32)
            .WithMessage("Sono consentite al massimo 32 squadre.");
    }
}

public sealed record AuctionPageRequest(int Page, int PageSize);

public sealed record AuctionCatalogRequest(string? Search, string? Role, int Page, int PageSize);
public sealed class AuctionCatalogRequestValidator : AbstractValidator<AuctionCatalogRequest>
{
    public AuctionCatalogRequestValidator()
    {
        RuleFor(x => x.Search).MaximumLength(200);
        RuleFor(x => x.Role).Must(role => role is null or "" or "P" or "D" or "C" or "A")
            .WithMessage("Ruolo Classic non valido.");
        RuleFor(x => x.Page).InclusiveBetween(1, 10000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class AuctionPageRequestValidator : AbstractValidator<AuctionPageRequest>
{
    public AuctionPageRequestValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 10000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
