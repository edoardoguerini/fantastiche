using Dapper;
using Fantastiche.Infrastructure.Catalog;

namespace Fantastiche.Infrastructure.Auctions;

internal sealed partial class AuctionReadSession
{
    private async Task<AuctionBombView?> ReadBombStateAsync(PlayerPhotoStorage photos, ClubLogoStorage logos, CancellationToken ct)
    {
        var parameters = new
        {
            SessionId = Session.Id,
            Session.LeagueSeasonId,
            Session.LeagueId,
            PhotoBaseUrl = photos.PublicBaseUrl,
            ClubLogoBaseUrl = logos.PublicBaseUrl,
            context.UserId
        };
        var bomb = await connection.QuerySingleOrDefaultAsync<BombAuction>(new CommandDefinition($"""
            SELECT TOP (1) {AuctionEngine.BombColumns} FROM BombAuctions bomb
            WHERE SessionId = @SessionId AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
              AND NOT EXISTS (SELECT 1 FROM PlayerAuctions pa WHERE pa.SessionId = bomb.SessionId
                  AND pa.LeagueSeasonId = bomb.LeagueSeasonId AND pa.LeagueId = bomb.LeagueId
                  AND pa.StartedAt > bomb.StartedAt AND (bomb.PlayerAuctionId IS NULL OR pa.Id <> bomb.PlayerAuctionId))
            ORDER BY StartedAt DESC, Id DESC
            """, parameters, transaction, cancellationToken: ct));
        if (bomb is null) return null;
        var player = await connection.QuerySingleAsync<BombPlayerRow>(new CommandDefinition($"""
            SELECT entry.Name, entry.ClubName, {PlayerPhotoStorage.SqlProjection}, {ClubLogoStorage.SqlProjection}
            FROM ListEntries entry
            INNER JOIN Players player ON player.Id = entry.PlayerId
            LEFT JOIN PlayerMedia media ON media.Source = player.Source AND media.ExternalId = player.ExternalId
            INNER JOIN Clubs club ON club.Id = entry.ClubId
            LEFT JOIN ClubMedia clubMedia ON clubMedia.Source = club.Source AND clubMedia.NormalizedClubName = club.NormalizedName
            WHERE entry.ListVersionId = @ListVersionId AND entry.PlayerId = @PlayerId
            """, new { bomb.ListVersionId, bomb.PlayerId, PhotoBaseUrl = photos.PublicBaseUrl, ClubLogoBaseUrl = logos.PublicBaseUrl }, transaction, cancellationToken: ct));
        var args = new { bomb.Id, bomb.Round, bomb.LeagueSeasonId, bomb.LeagueId, bomb.RevealedCount, context.UserId };
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            SELECT TeamId, CAST(CASE WHEN Amount IS NULL THEN 0 ELSE 1 END AS bit) AS HasSubmitted FROM BombOffers
            WHERE BombAuctionId = @Id AND Round = @Round AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId
            ORDER BY Position;

            SELECT TOP (@RevealedCount) TeamId, Amount FROM BombOffers
            WHERE BombAuctionId = @Id AND Round = @Round AND LeagueSeasonId = @LeagueSeasonId AND LeagueId = @LeagueId AND Amount IS NOT NULL
            ORDER BY Amount, Position;

            SELECT offer.Amount FROM BombOffers offer
            INNER JOIN TeamMembers tm ON tm.TeamId = offer.TeamId AND tm.LeagueSeasonId = offer.LeagueSeasonId AND tm.LeagueId = offer.LeagueId
            INNER JOIN LeagueMembers lm ON lm.LeagueId = tm.LeagueId AND lm.UserId = tm.UserId AND lm.Status = 1
            WHERE offer.BombAuctionId = @Id AND offer.Round = @Round AND offer.LeagueSeasonId = @LeagueSeasonId AND offer.LeagueId = @LeagueId AND tm.UserId = @UserId;
            """, args, transaction, cancellationToken: ct));
        var participants = (await grid.ReadAsync<AuctionBombParticipantView>()).AsList();
        var revealed = (await grid.ReadAsync<AuctionBombOfferView>()).AsList();
        var own = await grid.ReadSingleOrDefaultAsync<int?>();
        var completed = bomb.Status == BombAuctionStatus.Completed;
        return new(bomb.Id, bomb.PlayerId, player.Name, bomb.Role, player.ClubName, player.PhotoUrl, player.ClubLogoUrl,
            bomb.CallerTeamId, bomb.Status.ToString(), bomb.Round, bomb.MinimumAmount, bomb.Deadline, bomb.RevealStartedAt,
            bomb.NextRevealAt, participants, revealed, own, completed ? bomb.PlayerAuctionId : null,
            completed ? bomb.WinningTeamId : null, completed ? bomb.WinningAmount : null);
    }

    private sealed class BombPlayerRow
    {
        public string Name { get; init; } = "";
        public string ClubName { get; init; } = "";
        public string? PhotoUrl { get; init; }
        public string? ClubLogoUrl { get; init; }
    }
}
