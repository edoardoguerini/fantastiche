using System.Data.Common;
using Dapper;

namespace Fantastiche.Infrastructure.Auctions;

public sealed partial class AuctionEngine
{
    public Task<AuctionCommandResult> ControlAsync(ControlAuctionSessionCommand request, CancellationToken ct)
    {
        var order = request.TeamOrder?.ToArray();
        return ExecuteCommandAsync(request.Context, request.SessionId, request.RequestId, "Control", new
        {
            Command = "Control",
            request.SessionId,
            request.Action,
            TeamOrder = order
        }, null, async (c, tx, s, _, token) =>
        {
            await AuthorizeAsync(c, tx, request.Context, s.LeagueId, true, token);
            if (request.Action is not ("Pause" or "Resume" or "SkipTurn" or "Reorder" or "Complete"))
                throw Error("auction.invalid_action", "Controllo non valido.", 400);
            if ((request.Action == "Pause" && s.Status == AuctionSessionStatus.Paused)
                || (request.Action == "Resume" && s.Status == AuctionSessionStatus.Active)
                || (request.Action == "Complete" && s.Status == AuctionSessionStatus.Completed)) return null;
            if (s.Status == AuctionSessionStatus.Completed) throw Error("auction.completed", "La sessione è conclusa.");
            if (await ReadOpenAsync(c, tx, s, token) is not null) throw Error("auction.player_in_progress", "Questo controllo è disponibile tra due giocatori.");
            switch (request.Action)
            {
                case "Pause": s.Status = AuctionSessionStatus.Paused; break;
                case "Resume": s.Status = AuctionSessionStatus.Active; break;
                case "Complete": s.Status = AuctionSessionStatus.Completed; break;
                case "SkipTurn": await AdvanceAsync(c, tx, s, token); break;
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

    private sealed class TurnCapacity
    {
        public int Position { get; set; }
        public int Total { get; set; }
    }

    private static async Task AdvanceAsync(DbConnection c, DbTransaction tx, AuctionSession s, CancellationToken ct)
    {
        var rules = await ReadRulesAsync(c, tx, s, ct);
        var maximum = rules.Goalkeepers + rules.Defenders + rules.Midfielders + rules.Forwards;
        var turns = (await c.QueryAsync<TurnCapacity>(Sql("""
            SELECT co.Position, COUNT(r.PlayerId) AS Total FROM CallOrderEntries co
            LEFT JOIN RosterEntries r ON r.TeamId = co.TeamId AND r.LeagueSeasonId = co.LeagueSeasonId AND r.LeagueId = co.LeagueId
            WHERE co.SessionId = @Id AND co.LeagueSeasonId = @LeagueSeasonId AND co.LeagueId = @LeagueId
            GROUP BY co.Position ORDER BY co.Position
            """, s, tx, ct))).ToArray();
        for (var offset = 1; offset <= turns.Length; offset++)
        {
            var position = (s.CurrentPosition + offset) % turns.Length;
            if (turns[position].Total >= maximum) continue;
            s.CurrentPosition = position;
            return;
        }
        s.Status = AuctionSessionStatus.Completed;
    }
}
