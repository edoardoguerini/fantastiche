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
    bool HasAccess);

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
                    END AS bit) AS [HasAccess]
                FROM Requested requested
                LEFT JOIN [AuctionSessions] session ON session.[Id] = requested.[SessionId];
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
