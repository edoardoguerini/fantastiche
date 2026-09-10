namespace Fantastiche.Infrastructure.Auctions;

public sealed record AuctionCommandResult(
    Guid RequestId,
    Guid SessionId,
    Guid? AuctionId,
    long Version,
    DateTimeOffset ServerTime,
    bool Accepted,
    int StatusCode = 200,
    string? ErrorCode = null,
    string? Message = null);

public sealed record AuctionSessionView(
    Guid Id,
    Guid LeagueId,
    Guid LeagueSeasonId,
    Guid ListVersionId,
    string Status,
    long Version,
    Guid? CurrentTeamId,
    IReadOnlyList<Guid> TeamOrder,
    AuctionPlayerView? CurrentAuction,
    IReadOnlyList<AuctionTeamView> Teams,
    DateTimeOffset ServerTime);

public sealed record AuctionPlayerView(
    Guid Id,
    Guid PlayerId,
    string Name,
    string Role,
    string ClubName,
    Guid CallerTeamId,
    Guid WinningTeamId,
    int CurrentAmount,
    int DurationSeconds,
    IReadOnlyList<int> Increments,
    DateTimeOffset Deadline,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? ClosedAt,
    string? PhotoUrl = null,
    string? ClubLogoUrl = null,
    DateTime? BirthDate = null,
    string? Nationality = null,
    string? PreferredFoot = null,
    int? CurrentQuotation = null,
    int? InitialQuotation = null,
    int? Fvm = null);

public sealed record AuctionTeamView(
    Guid Id,
    string Name,
    int Budget,
    int Goalkeepers,
    int Defenders,
    int Midfielders,
    int Forwards);

public sealed record AuctionPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record AuctionRoomView(
    Guid LeagueId,
    Guid LeagueSeasonId,
    Guid? MyTeamId,
    bool CanManage,
    Guid? SessionId,
    Guid? ListVersionId,
    IReadOnlyList<AuctionTeamView> Teams);

public sealed record AuctionCatalogPlayerView(
    Guid PlayerId,
    string Name,
    string Role,
    string ClubName,
    bool IsAvailable,
    Guid? TeamId,
    string? PhotoUrl = null,
    string? ClubLogoUrl = null,
    int? CurrentQuotation = null,
    int? InitialQuotation = null,
    int? Fvm = null,
    bool? IsTransferred = null);

public sealed record AuctionBidView(
    Guid Id,
    Guid PlayerAuctionId,
    Guid TeamId,
    Guid UserId,
    int Amount,
    int Sequence,
    DateTimeOffset AcceptedAt);

public sealed record AuctionRosterView(
    Guid PlayerId,
    Guid TeamId,
    Guid PlayerAuctionId,
    string Name,
    string Role,
    string ClubName,
    int Price,
    DateTimeOffset AcquiredAt,
    string? PhotoUrl = null,
    string? ClubLogoUrl = null);
