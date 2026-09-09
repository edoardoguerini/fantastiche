using Dapper;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class GetCatalogVersionsQueryHandler(FantasticheDbContext db)
    : IRequestHandler<GetCatalogVersionsQuery, CatalogPage<ListVersionView>>
{
    public async Task<CatalogPage<ListVersionView>> HandleAsync(GetCatalogVersionsQuery request, CancellationToken ct)
    {
        CatalogQueryRules.RequireAuthenticated(request.Context.UserId);
        CatalogQueryRules.ValidatePage(request.Page, request.PageSize);
        var seasonName = CatalogQueryRules.Optional(request.SeasonName, 50);
        var parameters = new
        {
            showDrafts = request.Context.IsSuperAdmin,
            seasonName,
            offset = (request.Page - 1) * request.PageSize,
            request.PageSize
        };
        var connection = db.Database.GetDbConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT COUNT(*)
            FROM ListVersions
            WHERE (@showDrafts = 1 OR Status = 1)
              AND (@seasonName IS NULL OR SeasonName = @seasonName);

            SELECT Id, SeasonName,
                   CAST(CASE Status WHEN 0 THEN N'Draft' ELSE N'Published' END AS nvarchar(20)) AS Status,
                   Source, ContentHash, EntryCount, CreatedAt, PublishedAt
            FROM ListVersions
            WHERE (@showDrafts = 1 OR Status = 1)
              AND (@seasonName IS NULL OR SeasonName = @seasonName)
            ORDER BY CreatedAt DESC, Id DESC
            OFFSET @offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters, cancellationToken: ct));
        var total = await result.ReadSingleAsync<int>();
        var items = (await result.ReadAsync<ListVersionView>()).ToList();
        return new CatalogPage<ListVersionView>(items, request.Page, request.PageSize, total);
    }
}
