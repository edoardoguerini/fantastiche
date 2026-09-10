using System.Data;
using System.Data.Common;
using Dapper;
using Fantastiche.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace Fantastiche.Infrastructure.Auctions;

public sealed partial class AuctionEngine
{
    internal const string BombColumns = "Id, SessionId, LeagueSeasonId, LeagueId, ListVersionId, PlayerId, CallerTeamId, Role, Status, Round, MinimumAmount, Deadline, StartedAt, RevealStartedAt, NextRevealAt, ClosedAt, RevealedCount, PlayerAuctionId, WinningTeamId, WinningAmount";

    private static async Task RequireNoBombAsync(DbConnection c, DbTransaction tx, AuctionSession s, CancellationToken ct)
    {
        if (await c.ExecuteScalarAsync<int>(Sql("SELECT COUNT(*) FROM BombAuctions WHERE SessionId = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Status < 2", s, tx, ct)) > 0)
            throw Error("auction.bomb_in_progress", "Attendi la conclusione della Bomba.");
    }

    private static Task<BombAuction?> ReadBombAsync(DbConnection c, DbTransaction tx, AuctionSession s, Guid id, CancellationToken ct) =>
        c.QuerySingleOrDefaultAsync<BombAuction>(Sql($"SELECT {BombColumns} FROM BombAuctions WHERE Id = @BombId AND SessionId = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId",
            new { BombId = id, s.Id, s.LeagueSeasonId, s.LeagueId }, tx, ct));

    public Task<AuctionCommandResult> StartBombAsync(StartBombCommand request, CancellationToken ct)
    {
        RequireId(request.PlayerId);
        return ExecuteCommandAsync(request.Context, request.SessionId, request.RequestId, "StartBomb", new
        { Command = "StartBomb", request.SessionId, request.PlayerId }, null, async (c, tx, s, now, token) =>
        {
            if (s.Status != AuctionSessionStatus.Active) throw Error("auction.not_active", "La sessione non è attiva.");
            await RequireNoBombAsync(c, tx, s, token);
            if (await ReadOpenAsync(c, tx, s, token) is not null) throw Error("auction.player_in_progress", "Attendi la chiusura del giocatore in corso.");
            var team = await ParticipantAsync(c, tx, s, request.Context, token);
            var progress = await AuctionTurnProgress.ReadAsync(c, tx, s.Id, s.LeagueId, s.LeagueSeasonId, token);
            var position = progress.FindPosition(s.CurrentPosition, false)
                ?? throw Error("auction.rosters_complete", "Le rose sono complete.");
            if (team != progress.Turns[position].TeamId) throw Error("auction.not_caller", "Può chiamare soltanto la squadra di turno.", 403);
            var role = await c.QuerySingleOrDefaultAsync<string>(Sql("SELECT Role FROM ListEntries WHERE ListVersionId = @ListVersionId AND PlayerId = @PlayerId", new { s.ListVersionId, request.PlayerId }, tx, token))
                ?? throw Error("resource.not_found", "Giocatore non presente nel listone della sessione.", 404);
            if (role != progress.Role) throw Error("auction.wrong_role", "Puoi chiamare soltanto calciatori del ruolo in corso: " + progress.Role + ".");
            if (await c.ExecuteScalarAsync<bool>(Sql("SELECT COALESCE(IsTransferred, 0) FROM ListEntries WHERE ListVersionId = @ListVersionId AND PlayerId = @PlayerId", new { s.ListVersionId, request.PlayerId }, tx, token)))
                throw Error("auction.player_transferred", "Il giocatore è ceduto e non può essere chiamato.");
            if (await c.ExecuteScalarAsync<int>(Sql("SELECT COUNT(*) FROM RosterEntries WHERE LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND PlayerId = @PlayerId", new { s.LeagueSeasonId, s.LeagueId, request.PlayerId }, tx, token)) > 0)
                throw Error("auction.player_unavailable", "Giocatore già acquistato nella stagione.");
            await ValidateCapacityAsync(c, tx, s, team, role, 1, token);
            var candidates = (await c.QueryAsync<Guid>(Sql("""
                SELECT co.TeamId FROM CallOrderEntries co
                WHERE co.SessionId = @Id AND co.LeagueSeasonId = @LeagueSeasonId AND co.LeagueId = @LeagueId
                AND EXISTS (SELECT 1 FROM TeamMembers tm INNER JOIN LeagueMembers lm ON lm.LeagueId = tm.LeagueId AND lm.UserId = tm.UserId AND lm.Status = 1
                    WHERE tm.TeamId = co.TeamId AND tm.LeagueSeasonId = co.LeagueSeasonId AND tm.LeagueId = co.LeagueId)
                ORDER BY co.Position
                """, s, tx, token))).ToArray();
            var participants = new List<Guid>();
            foreach (var candidate in candidates)
            {
                try { await ValidateCapacityAsync(c, tx, s, candidate, role, 1, token); participants.Add(candidate); }
                catch (DomainException error) when (error.Code is "auction.roster_full" or "auction.role_full" or "auction.insufficient_budget") { }
            }
            var bomb = new BombAuction
            {
                SessionId = s.Id,
                LeagueSeasonId = s.LeagueSeasonId,
                LeagueId = s.LeagueId,
                ListVersionId = s.ListVersionId,
                PlayerId = request.PlayerId,
                CallerTeamId = team,
                Role = role,
                StartedAt = now,
                Deadline = now.AddSeconds(60),
                Status = BombAuctionStatus.Waiting
            };
            await c.ExecuteAsync(Sql($"INSERT INTO BombAuctions ({BombColumns}) VALUES (@Id, @SessionId, @LeagueSeasonId, @LeagueId, @ListVersionId, @PlayerId, @CallerTeamId, @Role, @Status, @Round, @MinimumAmount, @Deadline, @StartedAt, @RevealStartedAt, @NextRevealAt, @ClosedAt, @RevealedCount, @PlayerAuctionId, @WinningTeamId, @WinningAmount)", bomb, tx, token));
            await InsertParticipantsAsync(c, tx, bomb, participants, token);
            s.CurrentPosition = position;
            await SaveSessionAsync(c, tx, s, token);
            return bomb.Id;
        }, ct);
    }

    public Task<AuctionCommandResult> BombBidAsync(SubmitBombOfferCommand request, CancellationToken ct)
    {
        RequireId(request.BombAuctionId);
        return ExecuteCommandAsync(request.Context, request.SessionId, request.RequestId, "BombBid", new
        { Command = "BombBid", request.SessionId, request.BombAuctionId, request.Round, request.Amount }, request.BombAuctionId, async (c, tx, s, now, token) =>
        {
            if (request.Amount < 1 || request.Round < 1) throw Error("auction.invalid_amount", "Turno e importo devono essere positivi.", 400);
            var bomb = await ReadBombAsync(c, tx, s, request.BombAuctionId, token) ?? throw Error("resource.not_found", "Bomba non disponibile.", 404);
            if (bomb.Status != BombAuctionStatus.Collecting) throw Error("auction.bomb_not_collecting", "La raccolta delle offerte non è attiva.");
            if (request.Round != bomb.Round) throw Error("auction.bomb_wrong_round", "Il turno della Bomba è cambiato.");
            if (now >= bomb.Deadline) throw Error("auction.expired", "L’offerta è arrivata dopo la scadenza.");
            var team = await ParticipantAsync(c, tx, s, request.Context, token);
            var offer = await c.QuerySingleOrDefaultAsync<BombOffer>(Sql("""
                SELECT BombAuctionId, Round, TeamId, LeagueSeasonId, LeagueId, Position, Amount, UserId, SubmittedAt FROM BombOffers
                WHERE BombAuctionId = @Id AND Round = @Round AND TeamId = @TeamId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
                """, new { bomb.Id, bomb.Round, TeamId = team, bomb.LeagueSeasonId, bomb.LeagueId }, tx, token))
                ?? throw Error("auction.bomb_not_participant", "La squadra non partecipa a questo turno della Bomba.", 403);
            if (offer.Amount is not null) throw Error("auction.bomb_already_submitted", "L’offerta è già confermata.");
            if (request.Amount < bomb.MinimumAmount) throw Error("auction.bid_too_low", "L’offerta è inferiore al minimo del turno.");
            await ValidateCapacityAsync(c, tx, s, team, bomb.Role, request.Amount, token);
            await c.ExecuteAsync(Sql("""
                UPDATE BombOffers SET Amount = @Amount, UserId = @UserId, SubmittedAt = @Now
                WHERE BombAuctionId = @Id AND Round = @Round AND TeamId = @TeamId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Amount IS NULL
                """, new { bomb.Id, bomb.Round, TeamId = team, bomb.LeagueSeasonId, bomb.LeagueId, request.Amount, request.Context.UserId, Now = now }, tx, token));
            if (await c.ExecuteScalarAsync<int>(Sql("SELECT COUNT(*) FROM BombOffers WHERE BombAuctionId = @Id AND Round = @Round AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Amount IS NULL", bomb, tx, token)) == 0)
                await BeginRevealAsync(c, tx, bomb, now, token);
            await SaveSessionAsync(c, tx, s, token);
            return bomb.Id;
        }, ct);
    }

    public Task<AuctionCommandResult> CancelBombAsync(CancelBombCommand request, CancellationToken ct)
    {
        RequireId(request.BombAuctionId);
        return ExecuteCommandAsync(request.Context, request.SessionId, request.RequestId, "CancelBomb", new
        { Command = "CancelBomb", request.SessionId, request.BombAuctionId }, request.BombAuctionId, async (c, tx, s, now, token) =>
        {
            await AuthorizeAsync(c, tx, request.Context, s.LeagueId, true, token);
            var bomb = await ReadBombAsync(c, tx, s, request.BombAuctionId, token) ?? throw Error("resource.not_found", "Bomba non disponibile.", 404);
            if (bomb.Status >= BombAuctionStatus.Completed) throw Error("auction.bomb_closed", "La Bomba è conclusa.");
            bomb.Status = BombAuctionStatus.Cancelled;
            bomb.ClosedAt = now;
            bomb.NextRevealAt = null;
            await SaveBombAsync(c, tx, bomb, token);
            await SaveSessionAsync(c, tx, s, token);
            return bomb.Id;
        }, ct);
    }

    private static async Task InsertParticipantsAsync(DbConnection c, DbTransaction tx, BombAuction bomb, IReadOnlyList<Guid> teams, CancellationToken ct)
    {
        for (var i = 0; i < teams.Count; i++)
            await c.ExecuteAsync(Sql("""
                INSERT INTO BombOffers (BombAuctionId, Round, TeamId, LeagueSeasonId, LeagueId, Position, Amount, UserId, SubmittedAt)
                VALUES (@Id, @Round, @TeamId, @LeagueSeasonId, @LeagueId, @Position, NULL, NULL, NULL)
                """, new { bomb.Id, bomb.Round, TeamId = teams[i], bomb.LeagueSeasonId, bomb.LeagueId, Position = i }, tx, ct));
    }

    private static Task SaveBombAsync(DbConnection c, DbTransaction tx, BombAuction bomb, CancellationToken ct) => c.ExecuteAsync(Sql("""
        UPDATE BombAuctions SET Status = @Status, Round = @Round, MinimumAmount = @MinimumAmount, Deadline = @Deadline,
            RevealStartedAt = @RevealStartedAt, NextRevealAt = @NextRevealAt, ClosedAt = @ClosedAt, RevealedCount = @RevealedCount,
            PlayerAuctionId = @PlayerAuctionId, WinningTeamId = @WinningTeamId, WinningAmount = @WinningAmount
        WHERE Id = @Id AND SessionId = @SessionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
        """, bomb, tx, ct));

    private static async Task BeginRevealAsync(DbConnection c, DbTransaction tx, BombAuction bomb, DateTimeOffset now, CancellationToken ct)
    {
        bomb.Status = BombAuctionStatus.Revealing;
        bomb.RevealStartedAt = now;
        bomb.NextRevealAt = now.AddSeconds(3);
        await SaveBombAsync(c, tx, bomb, ct);
    }

    public async Task<int> AdvanceBombsAsync(CancellationToken ct)
    {
        await using var c = Connection();
        await c.OpenAsync(ct);
        var due = (await c.QueryAsync<Guid>(new CommandDefinition("""
            SELECT TOP (100) Id FROM BombAuctions
            WHERE (Status < 1 AND Deadline <= TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'))
               OR (Status = 1 AND NextRevealAt <= TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'))
            ORDER BY COALESCE(NextRevealAt, Deadline), Id
            """, cancellationToken: ct))).ToArray();
        var advanced = 0;
        foreach (var id in due)
        {
            try { if (await AdvanceBombAsync(id, ct)) advanced++; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error) { logger.LogError(error, "Avanzamento della Bomba {BombAuctionId} non riuscito.", id); }
        }
        return advanced;
    }

    private async Task<bool> AdvanceBombAsync(Guid id, CancellationToken ct)
    {
        await using var c = Connection();
        await c.OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var original = await c.QuerySingleOrDefaultAsync<BombAuction>(Sql($"SELECT {BombColumns} FROM BombAuctions WHERE Id = @Id", new { Id = id }, tx, ct));
        if (original is null) return false;
        await AuctionSqlLock.AcquireAsync(c, tx, original.LeagueSeasonId, true, ct);
        var s = await c.QuerySingleAsync<AuctionSession>(Sql($"SELECT {SessionColumns} FROM AuctionSessions WHERE Id = @SessionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId", original, tx, ct));
        var bomb = (await ReadBombAsync(c, tx, s, id, ct))!;
        var now = await c.ExecuteScalarAsync<DateTimeOffset>(Sql(SqlNow, null, tx, ct));
        if (s.Status != AuctionSessionStatus.Active || bomb.Status >= BombAuctionStatus.Completed) return false;
        if (bomb.Status == BombAuctionStatus.Waiting)
        {
            if (now < bomb.Deadline) return false;
            bomb.Status = BombAuctionStatus.Collecting;
            bomb.Deadline = now.AddSeconds(60);
            await SaveBombAsync(c, tx, bomb, ct);
        }
        else if (bomb.Status == BombAuctionStatus.Collecting)
        {
            if (now < bomb.Deadline) return false;
            await BeginRevealAsync(c, tx, bomb, now, ct);
        }
        else
        {
            if (bomb.NextRevealAt is null || now < bomb.NextRevealAt) return false;
            var offers = (await c.QueryAsync<BombOffer>(Sql("""
                SELECT BombAuctionId, Round, TeamId, LeagueSeasonId, LeagueId, Position, Amount, UserId, SubmittedAt FROM BombOffers
                WHERE BombAuctionId = @Id AND Round = @Round AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Amount IS NOT NULL
                ORDER BY Amount, Position
                """, bomb, tx, ct))).ToArray();
            if (offers.Length == 0)
            {
                bomb.Status = BombAuctionStatus.NoSale;
                bomb.ClosedAt = now;
                bomb.NextRevealAt = null;
                await AdvanceAsync(c, tx, s, ct);
            }
            else if (bomb.RevealedCount < offers.Length)
            {
                bomb.RevealedCount++;
                bomb.NextRevealAt = now.AddSeconds(2);
                // L’ultima offerta resta visibile due secondi prima dell’esito o dello spareggio.
            }
            else if (offers.Count(o => o.Amount == offers[^1].Amount) == 1)
            {
                await AwardBombAsync(c, tx, s, bomb, offers[^1], now, ct);
            }
            else
            {
                var maximum = offers[^1].Amount!.Value;
                var tied = offers.Where(o => o.Amount == maximum).OrderBy(o => o.Position).Select(o => o.TeamId).ToArray();
                bomb.Round++;
                bomb.MinimumAmount = maximum;
                bomb.Status = BombAuctionStatus.Collecting;
                bomb.Deadline = now.AddSeconds(60);
                bomb.RevealStartedAt = null;
                bomb.NextRevealAt = null;
                bomb.RevealedCount = 0;
                await InsertParticipantsAsync(c, tx, bomb, tied, ct);
            }
            await SaveBombAsync(c, tx, bomb, ct);
        }
        await SaveSessionAsync(c, tx, s, ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private static async Task AwardBombAsync(DbConnection c, DbTransaction tx, AuctionSession s, BombAuction bomb, BombOffer winner, DateTimeOffset now, CancellationToken ct)
    {
        var auction = new PlayerAuction
        {
            SessionId = s.Id,
            LeagueSeasonId = s.LeagueSeasonId,
            LeagueId = s.LeagueId,
            ListVersionId = bomb.ListVersionId,
            PlayerId = bomb.PlayerId,
            CallerTeamId = bomb.CallerTeamId,
            WinningTeamId = winner.TeamId,
            Role = bomb.Role,
            Number = await c.ExecuteScalarAsync<int>(Sql("SELECT COALESCE(MAX(Number), 0) + 1 FROM PlayerAuctions WHERE SessionId = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId", s, tx, ct)),
            DurationSeconds = 5,
            IncrementOptionsJson = "[]",
            CurrentAmount = winner.Amount!.Value,
            BidSequence = 1,
            Deadline = now,
            Status = PlayerAuctionStatus.Closed,
            StartedAt = bomb.StartedAt,
            ClosedAt = now
        };
        await c.ExecuteAsync(Sql($"INSERT INTO PlayerAuctions ({PlayerColumns}) VALUES (@Id, @SessionId, @LeagueSeasonId, @LeagueId, @ListVersionId, @PlayerId, @Number, @CallerTeamId, @WinningTeamId, @Role, @DurationSeconds, @IncrementOptionsJson, @CurrentAmount, @BidSequence, @Deadline, @Status, @StartedAt, @ClosedAt)", auction, tx, ct));
        await InsertBidAsync(c, tx, auction, winner.UserId!.Value, winner.SubmittedAt!.Value, ct);
        var debited = await c.ExecuteAsync(Sql("UPDATE Teams SET Budget = Budget - @CurrentAmount WHERE Id = @WinningTeamId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Budget >= @CurrentAmount", auction, tx, ct));
        if (debited != 1) throw new InvalidOperationException($"Budget incoerente alla chiusura della Bomba {bomb.Id:D}.");
        await c.ExecuteAsync(Sql("""
            INSERT INTO RosterEntries (LeagueSeasonId, PlayerId, LeagueId, TeamId, PlayerAuctionId, Role, Price, AcquiredAt)
            VALUES (@LeagueSeasonId, @PlayerId, @LeagueId, @WinningTeamId, @Id, @Role, @CurrentAmount, @ClosedAt);
            INSERT INTO BudgetMovements (Id, LeagueSeasonId, LeagueId, TeamId, PlayerAuctionId, Amount, CreatedAt)
            VALUES (NEWID(), @LeagueSeasonId, @LeagueId, @WinningTeamId, @Id, -@CurrentAmount, @ClosedAt);
            """, auction, tx, ct));
        bomb.Status = BombAuctionStatus.Completed;
        bomb.PlayerAuctionId = auction.Id;
        bomb.WinningTeamId = winner.TeamId;
        bomb.WinningAmount = winner.Amount;
        bomb.ClosedAt = now;
        bomb.NextRevealAt = null;
        await AdvanceAsync(c, tx, s, ct);
    }
}
