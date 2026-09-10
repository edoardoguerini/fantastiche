using Dapper;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class GetAuctionCatalogQueryHandler(FantasticheDbContext db, PlayerPhotoStorage photos, ClubLogoStorage logos)
    : IRequestHandler<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>
{
    public async Task<AuctionPage<AuctionCatalogPlayerView>> HandleAsync(GetAuctionCatalogQuery request, CancellationToken ct)
    {
        AuctionReadSession.ValidatePage(request.Page, request.PageSize);
        var search = request.Search?.Trim();
        var role = request.Role?.Trim().ToUpperInvariant();
        if (search?.Length > 200 || role is not (null or "" or "P" or "D" or "C" or "A"))
            throw new DomainException("auction.invalid_query", "Filtri del catalogo non validi.");
        await using var read = await AuctionReadSession.OpenAsync(db, request.Context, request.SessionId, ct);
        var parameters = new
        {
            request.SessionId,
            read.Session.LeagueId,
            read.Session.LeagueSeasonId,
            Search = string.IsNullOrEmpty(search) ? null : "%" + CatalogQueryRules.EscapeLike(search) + "%",
            Role = string.IsNullOrEmpty(role) ? null : role,
            request.AvailableOnly,
            PhotoBaseUrl = photos.PublicBaseUrl,
            ClubLogoBaseUrl = logos.PublicBaseUrl,
            Skip = (request.Page - 1) * request.PageSize,
            request.PageSize
        };
        const string source = """
            FROM AuctionSessions session
            INNER JOIN ListEntries entry ON entry.ListVersionId = session.ListVersionId
            INNER JOIN Players player ON player.Id = entry.PlayerId
            LEFT JOIN PlayerMedia media ON media.Source = player.Source AND media.ExternalId = player.ExternalId
            INNER JOIN Clubs club ON club.Id = entry.ClubId
            LEFT JOIN ClubMedia clubMedia ON clubMedia.Source = club.Source AND clubMedia.NormalizedClubName = club.NormalizedName
            LEFT JOIN RosterEntries roster ON roster.PlayerId = entry.PlayerId
                AND roster.LeagueSeasonId = @LeagueSeasonId AND roster.LeagueId = @LeagueId
            WHERE session.Id = @SessionId AND session.LeagueId = @LeagueId AND session.LeagueSeasonId = @LeagueSeasonId
              AND (@Search IS NULL OR entry.Name LIKE @Search ESCAPE N'~' OR entry.FullName LIKE @Search ESCAPE N'~')
              AND (@Role IS NULL OR entry.Role = @Role)
            """;
        const string available = """
            COALESCE(entry.IsTransferred, 0) = 0 AND roster.PlayerId IS NULL AND NOT EXISTS (
                SELECT 1 FROM PlayerAuctions auction WHERE auction.PlayerId = entry.PlayerId
                AND auction.LeagueSeasonId = @LeagueSeasonId AND auction.LeagueId = @LeagueId AND auction.Status = 0)
            AND NOT EXISTS (SELECT 1 FROM BombAuctions bomb WHERE bomb.PlayerId = entry.PlayerId
                AND bomb.LeagueSeasonId = @LeagueSeasonId AND bomb.LeagueId = @LeagueId AND bomb.Status < 2)
            """;
        using var result = await read.Connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT COUNT(*) {source} AND (@AvailableOnly = 0 OR ({available}));
            SELECT entry.PlayerId, entry.Name, entry.Role, entry.ClubName,
                   CAST(CASE WHEN {available} THEN 1 ELSE 0 END AS bit) AS IsAvailable, roster.TeamId, {PlayerPhotoStorage.SqlProjection}, {ClubLogoStorage.SqlProjection},
                   entry.CurrentQuotation, entry.InitialQuotation, entry.Fvm, entry.IsTransferred
            {source} AND (@AvailableOnly = 0 OR ({available}))
            ORDER BY entry.Name, entry.PlayerId
            OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters, read.Transaction, cancellationToken: ct));
        var total = await result.ReadSingleAsync<int>();
        var items = (await result.ReadAsync<AuctionCatalogPlayerView>()).AsList();
        await read.CommitAsync(ct);
        return new(items, request.Page, request.PageSize, total);
    }
}
