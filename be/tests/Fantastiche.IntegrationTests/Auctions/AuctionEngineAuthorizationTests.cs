using Dapper;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.Data.SqlClient;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task RevokedSuperAdminCannotCreateSessionEvenWhenItsClaimIsStillPresent()
    {
        var external = await Seed();
        var target = await Seed();
        var staleAdmin = external.Users[0] with { IsSuperAdmin = true };
        await SetSuperAdmin(staleAdmin.UserId!.Value, true);
        var allowed = await Send<CreateAuctionSessionCommand, AuctionSessionView>(
            new(staleAdmin, target.LeagueId, target.SeasonId, target.Teams));
        Assert.Equal(target.SeasonId, allowed.LeagueSeasonId);
        await SetSuperAdmin(staleAdmin.UserId.Value, false);
        var anotherTarget = await Seed();
        var error = await Assert.ThrowsAsync<DomainException>(() => Send<CreateAuctionSessionCommand, AuctionSessionView>(
            new(staleAdmin, anotherTarget.LeagueId, anotherTarget.SeasonId, anotherTarget.Teams)));
        Assert.Equal(403, error.StatusCode);
        await using var sql = new SqlConnection(fixture.ConnectionString);
        Assert.Equal(0, await sql.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM AuctionSessions WHERE LeagueSeasonId = @SeasonId", new { anotherTarget.SeasonId }));
    }

    [Fact]
    public async Task RevokedSuperAdminCannotControlForeignLeagueOrReplayItsFormerPermission()
    {
        var external = await Seed();
        var target = await Seed();
        var session = await Create(target);
        var staleAdmin = external.Users[0] with { IsSuperAdmin = true };
        await SetSuperAdmin(staleAdmin.UserId!.Value, true);
        var pause = new ControlAuctionSessionCommand(staleAdmin, session.Id, Guid.NewGuid(), "Pause");
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(pause)).Accepted);
        var before = await State(target, session.Id);
        await SetSuperAdmin(staleAdmin.UserId.Value, false);
        foreach (var command in new[] { pause, pause with { RequestId = Guid.NewGuid(), Action = "Resume" } })
        {
            var error = await Assert.ThrowsAsync<DomainException>(() => Send<ControlAuctionSessionCommand, AuctionCommandResult>(command));
            Assert.Equal(403, error.StatusCode);
        }
        var after = await State(target, session.Id);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal("Paused", after.Status);
    }

    [Fact]
    public async Task RevokedSuperAdminFallsBackToActiveMembershipAndOrganizerPermission()
    {
        var data = await Seed();
        var session = await Create(data);
        var staleAdmin = data.Users[1] with { IsSuperAdmin = true };
        await SetSuperAdmin(staleAdmin.UserId!.Value, true);
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(
            new(staleAdmin, session.Id, Guid.NewGuid(), "Pause"))).Accepted);
        await SetSuperAdmin(staleAdmin.UserId.Value, false);
        var rejected = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(
            new(staleAdmin, session.Id, Guid.NewGuid(), "Resume"));
        Assert.False(rejected.Accepted);
        Assert.Equal(403, rejected.StatusCode);
        Assert.Equal(rejected, await Send<GetAuctionReceiptQuery, AuctionCommandResult>(
            new(staleAdmin, session.Id, rejected.RequestId)));
        Assert.True((await Send<ControlAuctionSessionCommand, AuctionCommandResult>(
            new(data.Users[0] with { IsSuperAdmin = true }, session.Id, Guid.NewGuid(), "Resume"))).Accepted);
    }

    private Task SetSuperAdmin(Guid userId, bool enabled) => Execute(enabled ? """
        INSERT INTO AspNetUserRoles (UserId, RoleId)
        SELECT @UserId, Id FROM AspNetRoles WHERE NormalizedName = N'SUPERADMIN'
        """ : """
        DELETE userRole FROM AspNetUserRoles userRole
        INNER JOIN AspNetRoles role ON role.Id = userRole.RoleId
        WHERE userRole.UserId = @UserId AND role.NormalizedName = N'SUPERADMIN'
        """, new { UserId = userId });
}
