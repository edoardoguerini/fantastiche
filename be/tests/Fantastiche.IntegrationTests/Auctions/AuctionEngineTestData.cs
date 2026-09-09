using Dapper;
using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    private sealed record Scenario(Guid LeagueId, Guid SeasonId, Guid[] Teams, RequestContext[] Users, Guid[] Players);

    private async Task<Scenario> Seed(int teamCount = 2, int budget = 10, int goalkeepers = 1, int defenders = 1)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        var league = new League { Name = "Asta " + Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow };
        var list = new ListVersion
        {
            SeasonName = "test",
            Source = "Synthetic",
            ContentHash = Guid.NewGuid().ToString("N"),
            EntryCount = 4,
            CreatedByUserId = fixture.Admin.UserId!.Value,
            CreatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow,
            Status = ListVersionStatus.Published
        };
        var season = new LeagueSeason
        {
            LeagueId = league.Id,
            Name = "test",
            ListVersionId = list.Id,
            Budget = budget,
            Goalkeepers = goalkeepers,
            Defenders = defenders,
            Midfielders = 0,
            Forwards = 0
        };
        var club = new Club { Name = "Club", NormalizedName = "CLUB" + Guid.NewGuid().ToString("N"), Source = "Synthetic" };
        db.AddRange(league, list, season, club);
        var teams = new List<Guid>();
        var users = new List<RequestContext>();
        for (var i = 0; i < teamCount; i++)
        {
            var user = new ApplicationUser { Email = Guid.NewGuid().ToString("N") + "@example.test", DisplayName = "Asta" };
            user.UserName = user.Email;
            user.NormalizedEmail = user.Email.ToUpperInvariant();
            user.NormalizedUserName = user.NormalizedEmail;
            var team = new Team { LeagueId = league.Id, LeagueSeasonId = season.Id, Name = "Team " + i, NormalizedName = "TEAM " + i, Budget = budget };
            db.AddRange(user, team, new LeagueMember { LeagueId = league.Id, UserId = user.Id, Status = MembershipStatus.Active, IsOrganizer = i == 0 },
                new TeamMember { TeamId = team.Id, LeagueId = league.Id, LeagueSeasonId = season.Id, UserId = user.Id });
            teams.Add(team.Id);
            users.Add(new RequestContext(user.Id, false, "engine-test"));
        }
        var players = new List<Guid>();
        foreach (var role in new[] { "P", "P", "D", "D" })
        {
            var player = new Player { Source = "Synthetic", ExternalId = Guid.NewGuid().ToString("N") };
            db.AddRange(player, new ListEntry
            {
                ListVersionId = list.Id,
                PlayerId = player.Id,
                ClubId = club.Id,
                Name = "Player " + players.Count,
                FullName = "Player",
                Role = role,
                ClubName = "Club",
                BirthDate = new DateTime(2000, 1, 1),
                Nationality = "Italia",
                PreferredFoot = "Destro"
            });
            players.Add(player.Id);
        }
        await db.SaveChangesAsync();
        return new Scenario(league.Id, season.Id, teams.ToArray(), users.ToArray(), players.ToArray());
    }

    private Task<AuctionSessionView> Create(Scenario data) => Send<CreateAuctionSessionCommand, AuctionSessionView>(
        new(data.Users[0], data.LeagueId, data.SeasonId, data.Teams));

    private Task<AuctionCommandResult> Start(Scenario data, Guid sessionId, int player = 0, int user = 0) =>
        Send<StartPlayerAuctionCommand, AuctionCommandResult>(new(data.Users[user], sessionId, Guid.NewGuid(), data.Players[player], 30, [1, 5]));

    private Task<AuctionSessionView> State(Scenario data, Guid sessionId) => Send<GetAuctionStateQuery, AuctionSessionView>(new(data.Users[0], sessionId));

    private async Task<T> Send<TRequest, T>(TRequest request) where TRequest : IRequest<T>
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRequestPublisher>().SendAsync<TRequest, T>(request);
    }

    private async Task<bool> Close(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AuctionEngine>().CloseAsync(id, default);
    }

    private async Task Execute(string sql, object args)
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.ExecuteAsync(sql, args);
    }

    private Task Expire(Guid id) => Execute("UPDATE PlayerAuctions SET Deadline = DATEADD(second, -1, TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')) WHERE Id = @id", new { id });
}
