using System.Data;
using Dapper;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class GetAuctionRoomQueryHandler(FantasticheDbContext db) : IRequestHandler<GetAuctionRoomQuery, AuctionRoomView>
{
    public async Task<AuctionRoomView> HandleAsync(GetAuctionRoomQuery request, CancellationToken ct)
    {
        AuctionReadSession.RequireAuthenticated(request.Context);
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await AuctionReadSession.RequireAccessAsync(connection, transaction, request.Context, request.LeagueId, request.LeagueSeasonId, ct);
            await AuctionSqlLock.AcquireAsync(connection, transaction, request.LeagueSeasonId, false, ct);
            await AuctionReadSession.RequireAccessAsync(connection, transaction, request.Context, request.LeagueId, request.LeagueSeasonId, ct);
            using var result = await connection.QueryMultipleAsync(new CommandDefinition("""
                SELECT season.LeagueId, season.Id AS LeagueSeasonId,
                       (SELECT tm.TeamId FROM TeamMembers tm
                        INNER JOIN LeagueMembers lm ON lm.LeagueId = tm.LeagueId AND lm.UserId = tm.UserId AND lm.Status = 1
                        WHERE tm.LeagueId = season.LeagueId AND tm.LeagueSeasonId = season.Id AND tm.UserId = @UserId) AS MyTeamId,
                       CAST(CASE WHEN EXISTS (
                           SELECT 1 FROM AspNetUserRoles ur INNER JOIN AspNetRoles role ON role.Id = ur.RoleId
                           WHERE ur.UserId = @UserId AND role.NormalizedName = N'SUPERADMIN')
                           OR EXISTS (SELECT 1 FROM LeagueMembers lm
                                      WHERE lm.LeagueId = season.LeagueId AND lm.UserId = @UserId AND lm.Status = 1 AND lm.IsOrganizer = 1)
                           THEN 1 ELSE 0 END AS bit) AS CanManage,
                       session.Id AS SessionId, COALESCE(session.ListVersionId, season.ListVersionId) AS ListVersionId
                FROM LeagueSeasons season
                OUTER APPLY (
                    SELECT TOP (1) s.Id, s.ListVersionId FROM AuctionSessions s
                    WHERE s.LeagueId = season.LeagueId AND s.LeagueSeasonId = season.Id
                    ORDER BY CASE WHEN s.Status < 2 THEN 0 ELSE 1 END, s.CreatedAt DESC, s.Id DESC
                ) session
                WHERE season.LeagueId = @LeagueId AND season.Id = @LeagueSeasonId;

                SELECT team.Id, team.Name, team.Budget,
                       SUM(CASE WHEN roster.Role = N'P' THEN 1 ELSE 0 END) AS Goalkeepers,
                       SUM(CASE WHEN roster.Role = N'D' THEN 1 ELSE 0 END) AS Defenders,
                       SUM(CASE WHEN roster.Role = N'C' THEN 1 ELSE 0 END) AS Midfielders,
                       SUM(CASE WHEN roster.Role = N'A' THEN 1 ELSE 0 END) AS Forwards
                FROM Teams team
                LEFT JOIN RosterEntries roster ON roster.TeamId = team.Id
                    AND roster.LeagueId = team.LeagueId AND roster.LeagueSeasonId = team.LeagueSeasonId
                WHERE team.LeagueId = @LeagueId AND team.LeagueSeasonId = @LeagueSeasonId
                  AND EXISTS (SELECT 1 FROM TeamMembers tm
                              INNER JOIN LeagueMembers lm ON lm.LeagueId = tm.LeagueId AND lm.UserId = tm.UserId AND lm.Status = 1
                              WHERE tm.TeamId = team.Id AND tm.LeagueId = team.LeagueId AND tm.LeagueSeasonId = team.LeagueSeasonId)
                GROUP BY team.Id, team.Name, team.Budget
                ORDER BY team.Name, team.Id;
                """, new { request.LeagueId, request.LeagueSeasonId, request.Context.UserId }, transaction, cancellationToken: ct));
            var header = await result.ReadSingleAsync<RoomHeader>();
            var teams = (await result.ReadAsync<AuctionTeamView>()).AsList();
            await transaction.CommitAsync(ct);
            return new(header.LeagueId, header.LeagueSeasonId, header.MyTeamId, header.CanManage, header.SessionId, header.ListVersionId, teams);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private sealed record RoomHeader(Guid LeagueId, Guid LeagueSeasonId, Guid? MyTeamId, bool CanManage, Guid? SessionId, Guid? ListVersionId);
}
