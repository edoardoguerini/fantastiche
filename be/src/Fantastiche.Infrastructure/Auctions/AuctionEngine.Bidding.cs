using System.Data.Common;
using System.Text.Json;
using Dapper;

namespace Fantastiche.Infrastructure.Auctions;

public sealed partial class AuctionEngine
{
    public Task<AuctionCommandResult> StartAsync(StartPlayerAuctionCommand request, CancellationToken ct)
    {
        RequireId(request.PlayerId);
        var increments = request.Increments?.ToArray() ?? [];
        return ExecuteCommandAsync(request.Context, request.SessionId, request.RequestId, "Start", new
        {
            Command = "Start",
            request.SessionId,
            request.PlayerId,
            request.DurationSeconds,
            Increments = increments
        }, null, async (c, tx, s, now, token) =>
        {
            if (request.DurationSeconds is < 5 or > 30 || request.DurationSeconds % 5 != 0 || increments.Length is < 1 or > 10
                || increments.Any(x => x is < 1 or > 1_000_000) || increments.Distinct().Count() != increments.Length)
                throw Error("auction.invalid_options", "Durata o incrementi non validi.", 400);
            if (s.Status != AuctionSessionStatus.Active) throw Error("auction.not_active", "La sessione non è attiva.");
            if (await ReadOpenAsync(c, tx, s, token) is not null) throw Error("auction.player_in_progress", "Attendi la chiusura del giocatore in corso.");
            var team = await ParticipantAsync(c, tx, s, request.Context, token);
            var caller = await c.QuerySingleAsync<Guid>(Sql("""
                SELECT TeamId FROM CallOrderEntries WHERE SessionId = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Position = @CurrentPosition
                """, s, tx, token));
            if (team != caller) throw Error("auction.not_caller", "Può chiamare soltanto la squadra di turno.", 403);
            var role = await c.QuerySingleOrDefaultAsync<string>(Sql("SELECT Role FROM ListEntries WHERE ListVersionId = @ListVersionId AND PlayerId = @PlayerId",
                new { s.ListVersionId, request.PlayerId }, tx, token)) ?? throw Error("resource.not_found", "Giocatore non presente nel listone della sessione.", 404);
            if (await c.ExecuteScalarAsync<int>(Sql("SELECT COUNT(*) FROM RosterEntries WHERE LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND PlayerId = @PlayerId",
                new { s.LeagueSeasonId, s.LeagueId, request.PlayerId }, tx, token)) != 0)
                throw Error("auction.player_unavailable", "Giocatore già acquistato nella stagione.");
            await ValidateCapacityAsync(c, tx, s, team, role, 1, token);
            var number = await c.ExecuteScalarAsync<int>(Sql("SELECT COALESCE(MAX(Number), 0) + 1 FROM PlayerAuctions WHERE SessionId = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId", s, tx, token));
            var auction = new PlayerAuction
            {
                Id = Guid.CreateVersion7(),
                SessionId = s.Id,
                LeagueSeasonId = s.LeagueSeasonId,
                LeagueId = s.LeagueId,
                ListVersionId = s.ListVersionId,
                PlayerId = request.PlayerId,
                Number = number,
                CallerTeamId = team,
                WinningTeamId = team,
                Role = role,
                DurationSeconds = request.DurationSeconds,
                IncrementOptionsJson = JsonSerializer.Serialize(increments),
                CurrentAmount = 1,
                BidSequence = 1,
                Deadline = now.AddSeconds(request.DurationSeconds),
                StartedAt = now
            };
            await c.ExecuteAsync(Sql("""
                INSERT INTO PlayerAuctions (Id, SessionId, LeagueSeasonId, LeagueId, ListVersionId, PlayerId, Number, CallerTeamId, WinningTeamId,
                    Role, DurationSeconds, IncrementOptionsJson, CurrentAmount, BidSequence, Deadline, Status, StartedAt, ClosedAt)
                VALUES (@Id, @SessionId, @LeagueSeasonId, @LeagueId, @ListVersionId, @PlayerId, @Number, @CallerTeamId, @WinningTeamId,
                    @Role, @DurationSeconds, @IncrementOptionsJson, @CurrentAmount, @BidSequence, @Deadline, 0, @StartedAt, NULL)
                """, auction, tx, token));
            await InsertBidAsync(c, tx, auction, request.Context.UserId!.Value, now, token);
            await SaveSessionAsync(c, tx, s, token);
            return auction.Id;
        }, ct);
    }

    public Task<AuctionCommandResult> BidAsync(PlaceBidCommand request, CancellationToken ct)
    {
        RequireId(request.PlayerAuctionId);
        return ExecuteCommandAsync(request.Context, request.SessionId, request.RequestId, "Bid", new
        {
            Command = "Bid",
            request.SessionId,
            request.PlayerAuctionId,
            request.Amount
        }, request.PlayerAuctionId, async (c, tx, s, now, token) =>
        {
            if (request.Amount <= 0) throw Error("auction.invalid_amount", "L’offerta deve essere positiva.", 400);
            if (s.Status != AuctionSessionStatus.Active) throw Error("auction.not_active", "La sessione non è attiva.");
            var auction = await c.QuerySingleOrDefaultAsync<PlayerAuction>(Sql($"""
                SELECT {PlayerColumns} FROM PlayerAuctions
                WHERE Id = @PlayerAuctionId AND SessionId = @SessionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
                """, new { request.PlayerAuctionId, request.SessionId, s.LeagueSeasonId, s.LeagueId }, tx, token))
                ?? throw Error("resource.not_found", "Asta del giocatore non disponibile.", 404);
            if (auction.Status != PlayerAuctionStatus.Open) throw Error("auction.closed", "L’asta del giocatore è conclusa.");
            if (now >= auction.Deadline) throw Error("auction.expired", "L’offerta è arrivata dopo la scadenza.");
            var team = await ParticipantAsync(c, tx, s, request.Context, token);
            if (request.Amount <= auction.CurrentAmount) throw Error("auction.bid_too_low", "L’offerta deve superare quella corrente.");
            await ValidateCapacityAsync(c, tx, s, team, auction.Role, request.Amount, token);
            auction.WinningTeamId = team;
            auction.CurrentAmount = request.Amount;
            auction.BidSequence++;
            auction.Deadline = now.AddSeconds(auction.DurationSeconds);
            await c.ExecuteAsync(Sql("""
                UPDATE PlayerAuctions SET WinningTeamId = @WinningTeamId, CurrentAmount = @CurrentAmount, BidSequence = @BidSequence, Deadline = @Deadline
                WHERE Id = @Id AND SessionId = @SessionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Status = 0
                """, auction, tx, token));
            await InsertBidAsync(c, tx, auction, request.Context.UserId!.Value, now, token);
            await SaveSessionAsync(c, tx, s, token);
            return auction.Id;
        }, ct);
    }

    private static Task InsertBidAsync(DbConnection c, DbTransaction tx, PlayerAuction auction, Guid userId, DateTimeOffset now, CancellationToken ct) =>
        c.ExecuteAsync(Sql("""
            INSERT INTO Bids (Id, PlayerAuctionId, LeagueSeasonId, LeagueId, TeamId, UserId, Amount, Sequence, AcceptedAt)
            VALUES (@Id, @PlayerAuctionId, @LeagueSeasonId, @LeagueId, @TeamId, @UserId, @Amount, @Sequence, @AcceptedAt)
            """, new
        {
            Id = Guid.CreateVersion7(),
            PlayerAuctionId = auction.Id,
            auction.LeagueSeasonId,
            auction.LeagueId,
            TeamId = auction.WinningTeamId,
            UserId = userId,
            Amount = auction.CurrentAmount,
            Sequence = auction.BidSequence,
            AcceptedAt = now
        }, tx, ct));
}
