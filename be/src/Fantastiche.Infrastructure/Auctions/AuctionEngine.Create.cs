using System.Data;
using Dapper;
using Fantastiche.Infrastructure.Leagues;

namespace Fantastiche.Infrastructure.Auctions;

public sealed partial class AuctionEngine
{
    public async Task<AuctionSessionView> CreateSessionAsync(CreateAuctionSessionCommand request, CancellationToken ct)
    {
        RequireUser(request.Context);
        RequireId(request.LeagueId);
        RequireId(request.LeagueSeasonId);
        var order = request.TeamOrder?.ToArray() ?? [];
        if (order.Length is < 1 or > 32 || order.Contains(Guid.Empty) || order.Distinct().Count() != order.Length)
            throw Error("auction.invalid_order", "Specifica da 1 a 32 squadre distinte.", 400);
        var id = Guid.CreateVersion7();
        await using (var c = Connection())
        {
            await c.OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            await AuctionSqlLock.AcquireAsync(c, tx, request.LeagueSeasonId, true, ct);
            var season = await c.QuerySingleOrDefaultAsync<LeagueSeason>(Sql("""
                SELECT Id, LeagueId, Name, ListVersionId, Budget, Goalkeepers, Defenders, Midfielders, Forwards
                FROM LeagueSeasons WHERE Id = @LeagueSeasonId AND LeagueId = @LeagueId
                """, request, tx, ct)) ?? throw Error("resource.not_found", "Stagione non disponibile.", 404);
            await AuthorizeAsync(c, tx, request.Context, request.LeagueId, true, ct);
            if (await c.ExecuteScalarAsync<int>(Sql("SELECT COUNT(*) FROM AuctionSessions WHERE LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Status < 2", request, tx, ct)) != 0)
                throw Error("auction.session_in_progress", "Esiste già una sessione attiva per la stagione.");
            if (season.ListVersionId is null || await c.ExecuteScalarAsync<int>(Sql("SELECT COUNT(*) FROM ListVersions WHERE Id = @Id AND Status = 1", new { Id = season.ListVersionId }, tx, ct)) == 0)
                throw Error("auction.catalog_required", "Seleziona un listone pubblicato prima di avviare la sessione.");
            var teams = (await c.QueryAsync<Guid>(Sql("""
                SELECT t.Id FROM Teams t WHERE t.LeagueSeasonId = @LeagueSeasonId AND t.LeagueId = @LeagueId AND t.Id IN @Order
                AND EXISTS (SELECT 1 FROM TeamMembers tm INNER JOIN LeagueMembers lm ON lm.LeagueId = tm.LeagueId AND lm.UserId = tm.UserId
                    WHERE tm.TeamId = t.Id AND tm.LeagueSeasonId = t.LeagueSeasonId AND tm.LeagueId = t.LeagueId AND lm.Status = 1)
                """, new { request.LeagueSeasonId, request.LeagueId, Order = order }, tx, ct))).ToArray();
            if (teams.Length != order.Length) throw Error("auction.invalid_participants", "Tutte le squadre devono appartenere alla stagione e avere un membro attivo.");
            var now = await c.ExecuteScalarAsync<DateTimeOffset>(Sql(SqlNow, null, tx, ct));
            await c.ExecuteAsync(Sql("""
                INSERT INTO AuctionSessions (Id, LeagueId, LeagueSeasonId, ListVersionId, Status, CurrentPosition, Version, CreatedAt, CreatedByUserId)
                VALUES (@Id, @LeagueId, @LeagueSeasonId, @ListVersionId, 0, @CurrentPosition, 1, @CreatedAt, @CreatedByUserId)
                """, new
            {
                Id = id,
                request.LeagueId,
                request.LeagueSeasonId,
                season.ListVersionId,
                CurrentPosition = 0,
                CreatedAt = now,
                CreatedByUserId = request.Context.UserId
            }, tx, ct));
            for (var i = 0; i < order.Length; i++)
                await c.ExecuteAsync(Sql("""
                    INSERT INTO CallOrderEntries (SessionId, TeamId, LeagueSeasonId, LeagueId, Position)
                    VALUES (@SessionId, @TeamId, @LeagueSeasonId, @LeagueId, @Position)
                    """, new { SessionId = id, TeamId = order[i], request.LeagueSeasonId, request.LeagueId, Position = i }, tx, ct));
            var progress = await AuctionTurnProgress.ReadAsync(c, tx, id, request.LeagueId, request.LeagueSeasonId, ct);
            var position = progress.FindPosition(0, false)
                ?? throw Error("auction.rosters_complete", "Le rose delle squadre partecipanti sono già complete.");
            await c.ExecuteAsync(Sql("UPDATE AuctionSessions SET CurrentPosition = @position WHERE Id = @id AND LeagueId = @LeagueId AND LeagueSeasonId = @LeagueSeasonId",
                new { id, position, request.LeagueId, request.LeagueSeasonId }, tx, ct));
            await tx.CommitAsync(ct);
        }
        return await publisher.QueryAsync<GetAuctionStateQuery, AuctionSessionView>(new(request.Context, id), ct);
    }
}
