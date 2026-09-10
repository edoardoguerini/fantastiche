using System.Data;
using System.Text.Json;
using Dapper;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record AuctionObservationRequest(
    string ConnectionId,
    Guid SessionId,
    Guid UserId);

public sealed record AuctionObservation(
    string ConnectionId,
    Guid SessionId,
    long Version,
    bool HasAccess,
    Guid UserId,
    string DisplayName,
    string? TeamName,
    bool IsOrganizer);

public sealed class ObserveAuctionsQuery(FantasticheDbContext db)
{
    public async Task<IReadOnlyList<AuctionObservation>> ExecuteAsync(
        IReadOnlyCollection<AuctionObservationRequest> observers,
        CancellationToken cancellationToken)
    {
        if (observers.Count == 0)
        {
            return [];
        }

        var parametersJson = JsonSerializer.Serialize(observers);
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == ConnectionState.Closed;
        if (shouldClose)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            var rows = await connection.QueryAsync<AuctionObservation>(new CommandDefinition("""
                WITH Requested AS
                (
                    SELECT [ConnectionId], [SessionId], [UserId]
                    FROM OPENJSON(@ParamsJson)
                    WITH
                    (
                        [ConnectionId] nvarchar(256) '$.ConnectionId',
                        [SessionId] uniqueidentifier '$.SessionId',
                        [UserId] uniqueidentifier '$.UserId'
                    )
                )
                SELECT
                    requested.[ConnectionId],
                    requested.[SessionId],
                    COALESCE(session.[Version], CAST(0 AS bigint)) AS [Version],
                    CAST(CASE
                        WHEN session.[Id] IS NOT NULL AND
                        (
                            EXISTS
                            (
                                SELECT 1
                                FROM [LeagueMembers] member
                                WHERE member.[LeagueId] = session.[LeagueId]
                                  AND member.[UserId] = requested.[UserId]
                                  AND member.[Status] = 1
                            )
                            OR EXISTS
                            (
                                SELECT 1
                                FROM [AspNetUserRoles] userRole
                                INNER JOIN [AspNetRoles] role ON role.[Id] = userRole.[RoleId]
                                WHERE userRole.[UserId] = requested.[UserId]
                                  AND role.[NormalizedName] = N'SUPERADMIN'
                            )
                        ) THEN 1 ELSE 0
                    END AS bit) AS [HasAccess],
                    requested.[UserId],
                    COALESCE(account.[DisplayName], N'') AS [DisplayName],
                    team.[Name] AS [TeamName],
                    CAST(COALESCE(member.[IsOrganizer], 0) AS bit) AS [IsOrganizer]
                FROM Requested requested
                LEFT JOIN [AuctionSessions] session ON session.[Id] = requested.[SessionId]
                LEFT JOIN [AspNetUsers] account ON account.[Id] = requested.[UserId]
                LEFT JOIN [LeagueMembers] member ON member.[UserId] = requested.[UserId]
                    AND member.[LeagueId] = session.[LeagueId] AND member.[Status] = 1
                LEFT JOIN [TeamMembers] tm ON tm.[UserId] = member.[UserId]
                    AND tm.[LeagueId] = session.[LeagueId] AND tm.[LeagueSeasonId] = session.[LeagueSeasonId]
                LEFT JOIN [Teams] team ON team.[Id] = tm.[TeamId]
                    AND team.[LeagueId] = session.[LeagueId] AND team.[LeagueSeasonId] = session.[LeagueSeasonId];
                """, new { ParamsJson = parametersJson }, cancellationToken: cancellationToken));
            return rows.AsList();
        }
        finally
        {
            if (shouldClose)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }
}
