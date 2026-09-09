using System.Data.Common;
using Dapper;

namespace Fantastiche.Infrastructure.Auctions;

public static class AuctionSqlLock
{
    public static Task AcquireAsync(DbConnection connection, DbTransaction transaction, Guid leagueSeasonId,
        bool exclusive, CancellationToken ct) => connection.ExecuteAsync(new CommandDefinition("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = @Resource, @LockMode = @Mode,
                @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51000, 'Auction busy', 1;
            """, new { Resource = $"Fantastiche:Auction:{leagueSeasonId:D}", Mode = exclusive ? "Exclusive" : "Shared" },
            transaction, cancellationToken: ct));
}
