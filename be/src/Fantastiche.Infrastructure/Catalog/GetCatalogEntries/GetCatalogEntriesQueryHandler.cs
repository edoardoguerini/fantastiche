using Dapper;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class GetCatalogEntriesQueryHandler(FantasticheDbContext db, PlayerPhotoStorage photos, ClubLogoStorage logos)
    : IRequestHandler<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>
{
    public async Task<CatalogPage<CatalogEntryView>> HandleAsync(GetCatalogEntriesQuery request, CancellationToken ct)
    {
        CatalogQueryRules.RequireAuthenticated(request.Context.UserId);
        CatalogQueryRules.ValidatePage(request.Page, request.PageSize);
        var search = CatalogQueryRules.Optional(request.Search, 100);
        var role = CatalogQueryRules.Optional(request.Role, 1)?.ToUpperInvariant();
        var club = CatalogQueryRules.Optional(request.Club, 100);
        if (role is not null and not ("P" or "D" or "C" or "A"))
            throw CatalogQueryRules.InvalidQuery();
        var connection = db.Database.GetDbConnection();
        var status = await connection.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT Status FROM ListVersions WHERE Id = @id;",
            new { id = request.ListVersionId },
            cancellationToken: ct));
        if (status is null || status == (int)ListVersionStatus.Draft && !await CatalogAuthorization.IsSuperAdminAsync(db, request.Context, ct))
            throw CatalogQueryRules.NotFound();

        var parameters = new
        {
            id = request.ListVersionId,
            search = search is null ? null : "%" + CatalogQueryRules.EscapeLike(search) + "%",
            role,
            club,
            offset = (request.Page - 1) * request.PageSize,
            PhotoBaseUrl = photos.PublicBaseUrl,
            ClubLogoBaseUrl = logos.PublicBaseUrl,
            request.PageSize
        };
        const string where = """
            e.ListVersionId = @id
            AND (@search IS NULL OR e.Name LIKE @search ESCAPE N'~' OR e.FullName LIKE @search ESCAPE N'~')
            AND (@role IS NULL OR e.Role = @role)
            AND (@club IS NULL OR e.ClubName = @club)
            """;
        using var result = await connection.QueryMultipleAsync(new CommandDefinition($"""
            SELECT COUNT(*)
            FROM ListEntries e
            WHERE {where};

            SELECT e.PlayerId, p.ExternalId, e.Name, e.FullName, e.Role, e.ClubName,
                   e.BirthDate, e.Nationality, e.PreferredFoot, {PlayerPhotoStorage.SqlProjection}, {ClubLogoStorage.SqlProjection},
                   e.MantraRole, e.CurrentQuotation, e.InitialQuotation, e.CurrentMantraQuotation, e.InitialMantraQuotation, e.Fvm, e.MantraFvm, e.IsTransferred
            FROM ListEntries e
            INNER JOIN Players p ON p.Id = e.PlayerId
            LEFT JOIN PlayerMedia media ON media.Source = p.Source AND media.ExternalId = p.ExternalId
            INNER JOIN Clubs club ON club.Id = e.ClubId
            LEFT JOIN ClubMedia clubMedia ON clubMedia.Source = club.Source AND clubMedia.NormalizedClubName = club.NormalizedName
            WHERE {where}
            ORDER BY e.Name, e.PlayerId
            OFFSET @offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters, cancellationToken: ct));
        var total = await result.ReadSingleAsync<int>();
        var items = (await result.ReadAsync<CatalogEntryView>()).ToList();
        return new CatalogPage<CatalogEntryView>(items, request.Page, request.PageSize, total);
    }
}
