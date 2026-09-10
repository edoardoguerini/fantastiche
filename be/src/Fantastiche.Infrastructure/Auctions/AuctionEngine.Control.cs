using System.Data.Common;
using Dapper;

namespace Fantastiche.Infrastructure.Auctions;

public sealed partial class AuctionEngine
{
    public Task<AuctionCommandResult> ControlAsync(ControlAuctionSessionCommand request, CancellationToken ct)
    {
        var order = request.TeamOrder?.ToArray();
        // Conserva l’hash dei comandi già salvati prima dell’introduzione di GoToTurn.
        object payload = request.Action == "GoToTurn" ? new
        {
            Command = "Control",
            request.SessionId,
            request.Action,
            TeamOrder = order,
            request.TargetTeamId
        } : new
        {
            Command = "Control",
            request.SessionId,
            request.Action,
            TeamOrder = order
        };
        return ExecuteCommandAsync(request.Context, request.SessionId, request.RequestId, "Control", payload, null, async (c, tx, s, _, token) =>
        {
            await AuthorizeAsync(c, tx, request.Context, s.LeagueId, true, token);
            if (request.Action is not ("Pause" or "Resume" or "SkipTurn" or "GoToTurn" or "Reorder" or "Complete"))
                throw Error("auction.invalid_action", "Controllo non valido.", 400);
            await RequireNoBombAsync(c, tx, s, token);
            if ((request.Action == "Pause" && s.Status == AuctionSessionStatus.Paused)
                || (request.Action == "Resume" && s.Status == AuctionSessionStatus.Active)
                || (request.Action == "Complete" && s.Status == AuctionSessionStatus.Completed)) return null;
            if (s.Status == AuctionSessionStatus.Completed) throw Error("auction.completed", "La sessione è conclusa.");
            if (await ReadOpenAsync(c, tx, s, token) is not null) throw Error("auction.player_in_progress", "Questo controllo è disponibile tra due giocatori.");
            var currentProgress = await AuctionTurnProgress.ReadAsync(c, tx, s.Id, s.LeagueId, s.LeagueSeasonId, token);
            var currentPosition = currentProgress.FindPosition(s.CurrentPosition, false);
            if (currentPosition is not null) s.CurrentPosition = currentPosition.Value;
            switch (request.Action)
            {
                case "Pause": s.Status = AuctionSessionStatus.Paused; break;
                case "Resume": s.Status = AuctionSessionStatus.Active; break;
                case "Complete": s.Status = AuctionSessionStatus.Completed; break;
                case "SkipTurn": await AdvanceAsync(c, tx, s, token); break;
                case "GoToTurn":
                    var progress = currentProgress;
                    var target = progress.Turns.SingleOrDefault(turn => turn.TeamId == request.TargetTeamId);
                    if (target is null) throw Error("auction.invalid_target", "Scegli una squadra partecipante.", 400);
                    if (target.Full) throw Error("auction.roster_full", "La squadra ha già completato la rosa.");
                    if (progress.Role is null || !target.HasSpace(progress.Role))
                        throw Error("auction.role_full", "La squadra ha completato il ruolo in corso e salta il turno.");
                    if (s.CurrentPosition == target.Position) return null;
                    s.CurrentPosition = target.Position;
                    break;
                case "Reorder":
                    var previous = (await c.QueryAsync<Guid>(Sql("""
                        SELECT TeamId FROM CallOrderEntries WHERE SessionId = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId ORDER BY Position
                        """, s, tx, token))).ToArray();
                    if (order is null || order.Length != previous.Length || order.Distinct().Count() != order.Length || !order.ToHashSet().SetEquals(previous))
                        throw Error("auction.invalid_order", "Il nuovo ordine deve conservare esattamente le squadre partecipanti.");
                    if (order.SequenceEqual(previous)) return null;
                    var caller = previous[s.CurrentPosition];
                    await c.ExecuteAsync(Sql("DELETE FROM CallOrderEntries WHERE SessionId = @Id AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId", s, tx, token));
                    for (var i = 0; i < order.Length; i++)
                        await c.ExecuteAsync(Sql("""
                            INSERT INTO CallOrderEntries (SessionId, TeamId, LeagueSeasonId, LeagueId, Position)
                            VALUES (@SessionId, @TeamId, @LeagueSeasonId, @LeagueId, @Position)
                            """, new { SessionId = s.Id, TeamId = order[i], s.LeagueSeasonId, s.LeagueId, Position = i }, tx, token));
                    s.CurrentPosition = Array.IndexOf(order, caller);
                    break;
            }
            await SaveSessionAsync(c, tx, s, token);
            return null;
        }, ct);
    }

    private static async Task AdvanceAsync(DbConnection c, DbTransaction tx, AuctionSession s, CancellationToken ct)
    {
        var progress = await AuctionTurnProgress.ReadAsync(c, tx, s.Id, s.LeagueId, s.LeagueSeasonId, ct);
        var next = progress.FindPosition(s.CurrentPosition, true);
        if (next is null) s.Status = AuctionSessionStatus.Completed;
        else s.CurrentPosition = next.Value;
    }
}
