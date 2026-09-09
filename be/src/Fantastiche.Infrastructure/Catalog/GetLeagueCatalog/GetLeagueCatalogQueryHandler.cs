using Dapper;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class GetLeagueCatalogQueryHandler(FantasticheDbContext db)
    : IRequestHandler<GetLeagueCatalogQuery, ListVersionView?>
{
    public async Task<ListVersionView?> HandleAsync(GetLeagueCatalogQuery request, CancellationToken ct)
    {
        CatalogQueryRules.RequireAuthenticated(request.Context.UserId);
        var connection = db.Database.GetDbConnection();
        var season = await connection.QuerySingleOrDefaultAsync<LeagueSeasonCatalogState>(new CommandDefinition("""
            SELECT ListVersionId
            FROM LeagueSeasons
            WHERE Id = @seasonId AND LeagueId = @leagueId;
            """, new { seasonId = request.LeagueSeasonId, leagueId = request.LeagueId }, cancellationToken: ct));
        if (season is null) throw CatalogQueryRules.NotFound();

        if (!request.Context.IsSuperAdmin)
        {
            var member = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                SELECT COUNT(*)
                FROM LeagueMembers
                WHERE LeagueId = @leagueId AND UserId = @userId AND Status = 1;
                """, new { leagueId = request.LeagueId, userId = request.Context.UserId }, cancellationToken: ct));
            if (member == 0) throw CatalogQueryRules.Forbidden();
        }

        if (season.ListVersionId is null) return null;
        return await connection.QuerySingleAsync<ListVersionView>(new CommandDefinition("""
            SELECT Id, SeasonName,
                   CAST(CASE Status WHEN 0 THEN N'Draft' ELSE N'Published' END AS nvarchar(20)) AS Status,
                   Source, ContentHash, EntryCount, CreatedAt, PublishedAt
            FROM ListVersions
            WHERE Id = @id AND Status = 1;
            """, new { id = season.ListVersionId }, cancellationToken: ct));
    }

    private sealed class LeagueSeasonCatalogState
    {
        public Guid? ListVersionId { get; init; }
    }
}
