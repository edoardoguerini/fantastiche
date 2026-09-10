using System.Data.Common;
using Dapper;

namespace Fantastiche.Infrastructure.Auctions;

// La fase deriva dalle rose stagionali dei partecipanti, anche nelle sessioni successive.
internal sealed class AuctionTurnProgress(AuctionTurnProgress.Turn[] turns)
{
    private static readonly string[] Roles = ["P", "D", "C", "A"];
    internal Turn[] Turns { get; } = turns;
    internal string? Role { get; } = Roles.FirstOrDefault(role => turns.Any(turn => turn.HasSpace(role)));

    internal int? FindPosition(int current, bool advance)
    {
        if (Role is null) return null;
        for (var offset = advance ? 1 : 0; offset < Turns.Length + (advance ? 1 : 0); offset++)
        {
            var position = (current + offset + Turns.Length) % Turns.Length;
            if (Turns[position].HasSpace(Role)) return position;
        }
        return null;
    }

    internal sealed class Turn
    {
        public Guid TeamId { get; set; }
        public int Position { get; set; }
        public int Goalkeepers { get; set; }
        public int Defenders { get; set; }
        public int Midfielders { get; set; }
        public int Forwards { get; set; }
        public int GoalkeeperLimit { get; set; }
        public int DefenderLimit { get; set; }
        public int MidfielderLimit { get; set; }
        public int ForwardLimit { get; set; }
        internal bool HasSpace(string role) => role switch
        {
            "P" => Goalkeepers < GoalkeeperLimit,
            "D" => Defenders < DefenderLimit,
            "C" => Midfielders < MidfielderLimit,
            "A" => Forwards < ForwardLimit,
            _ => false
        };
        internal bool Full => !Roles.Any(HasSpace);
    }

    internal static async Task<AuctionTurnProgress> ReadAsync(DbConnection connection, DbTransaction transaction,
        Guid sessionId, Guid leagueId, Guid leagueSeasonId, CancellationToken ct)
    {
        var turns = await connection.QueryAsync<Turn>(new CommandDefinition("""
            SELECT co.TeamId, co.Position,
                SUM(CASE WHEN r.Role = 'P' THEN 1 ELSE 0 END) AS Goalkeepers,
                SUM(CASE WHEN r.Role = 'D' THEN 1 ELSE 0 END) AS Defenders,
                SUM(CASE WHEN r.Role = 'C' THEN 1 ELSE 0 END) AS Midfielders,
                SUM(CASE WHEN r.Role = 'A' THEN 1 ELSE 0 END) AS Forwards,
                ls.Goalkeepers AS GoalkeeperLimit, ls.Defenders AS DefenderLimit,
                ls.Midfielders AS MidfielderLimit, ls.Forwards AS ForwardLimit
            FROM CallOrderEntries co
            INNER JOIN LeagueSeasons ls ON ls.Id = co.LeagueSeasonId AND ls.LeagueId = co.LeagueId
            LEFT JOIN RosterEntries r ON r.TeamId = co.TeamId AND r.LeagueSeasonId = co.LeagueSeasonId AND r.LeagueId = co.LeagueId
            WHERE co.SessionId = @sessionId AND co.LeagueId = @leagueId AND co.LeagueSeasonId = @leagueSeasonId
            GROUP BY co.TeamId, co.Position, ls.Goalkeepers, ls.Defenders, ls.Midfielders, ls.Forwards
            ORDER BY co.Position
            """, new { sessionId, leagueId, leagueSeasonId }, transaction, cancellationToken: ct));
        return new(turns.ToArray());
    }
}
