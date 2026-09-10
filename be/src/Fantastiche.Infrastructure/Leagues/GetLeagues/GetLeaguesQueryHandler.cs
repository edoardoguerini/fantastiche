using Dapper;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Leagues;

public sealed class GetLeaguesQueryHandler(FantasticheDbContext db)
    : IRequestHandler<GetLeaguesQuery, LeaguePage<LeagueDetails>>
{
    public async Task<LeaguePage<LeagueDetails>> HandleAsync(GetLeaguesQuery request, CancellationToken ct)
    {
        if (request.Context.UserId is null || request.Context.UserId == Guid.Empty)
            throw new DomainException("auth.required", "Accesso richiesto.", 401);
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
            throw new DomainException("league.invalid_query", "Paginazione non valida.");

        var connection = db.Database.GetDbConnection();
        var isSuperAdmin = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(*)
            FROM AspNetUserRoles userRole
            INNER JOIN AspNetRoles role ON role.Id = userRole.RoleId
            WHERE userRole.UserId = @userId
              AND role.NormalizedName = N'SUPERADMIN';
            """, new { userId = request.Context.UserId.Value }, cancellationToken: ct)) > 0;
        var parameters = new
        {
            userId = request.Context.UserId.Value,
            isSuperAdmin,
            offset = ((long)request.Page - 1) * request.PageSize,
            request.PageSize
        };
        using var result = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT COUNT(*)
            FROM Leagues league
            CROSS APPLY
                (
                    SELECT TOP (1) innerSeason.Id
                    FROM LeagueSeasons innerSeason
                    WHERE innerSeason.LeagueId = league.Id
                    ORDER BY innerSeason.Id DESC
                ) season
            WHERE @isSuperAdmin = 1
               OR EXISTS
                  (
                      SELECT 1
                      FROM LeagueMembers member
                      WHERE member.LeagueId = league.Id
                        AND member.UserId = @userId
                        AND member.Status = 1
                  );

            SELECT league.Id,
                   league.Name,
                   season.Id AS LeagueSeasonId,
                   season.Name AS SeasonName,
                   season.Budget,
                   season.Goalkeepers,
                   season.Defenders,
                   season.Midfielders,
                   season.Forwards
            FROM Leagues league
            CROSS APPLY
                (
                    SELECT TOP (1)
                           innerSeason.Id,
                           innerSeason.Name,
                           innerSeason.Budget,
                           innerSeason.Goalkeepers,
                           innerSeason.Defenders,
                           innerSeason.Midfielders,
                           innerSeason.Forwards
                    FROM LeagueSeasons innerSeason
                    WHERE innerSeason.LeagueId = league.Id
                    ORDER BY innerSeason.Id DESC
                ) season
            WHERE @isSuperAdmin = 1
               OR EXISTS
                  (
                      SELECT 1
                      FROM LeagueMembers member
                      WHERE member.LeagueId = league.Id
                        AND member.UserId = @userId
                        AND member.Status = 1
                  )
            ORDER BY league.CreatedAt DESC, league.Id DESC, season.Id DESC
            OFFSET @offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """, parameters, cancellationToken: ct));
        var totalCount = await result.ReadSingleAsync<int>();
        var items = (await result.ReadAsync<LeagueDetails>()).ToList();
        return new LeaguePage<LeagueDetails>(items, totalCount, request.Page, request.PageSize);
    }
}
