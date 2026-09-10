using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Dapper;
using Fantastiche.Application.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task AuctionRealtimeHubRequiresAuthentication()
    {
        await using var factory = Factory();
        var socketClient = factory.Server.CreateWebSocketClient();
        socketClient.ConfigureRequest = request =>
            request.Headers.Origin = "http://localhost:6061";

        await Assert.ThrowsAnyAsync<Exception>(() => socketClient.ConnectAsync(
            new Uri("ws://localhost/hubs/Auctions"),
            CancellationToken.None));
    }

    [Fact]
    public async Task AuctionRealtimeHubRejectsAnUnlistedBrowserOrigin()
    {
        await using var factory = Factory();
        using var client = Client(factory);
        var cookie = await AuctionRealtimeLoginCookie(client, "admin@example.test", "Test-Admin-123!");
        var socketClient = factory.Server.CreateWebSocketClient();
        socketClient.ConfigureRequest = request =>
        {
            request.Headers.Cookie = cookie;
            request.Headers.Origin = "https://untrusted.example";
        };

        await Assert.ThrowsAnyAsync<Exception>(() => socketClient.ConnectAsync(
            new Uri("ws://localhost/hubs/Auctions"),
            CancellationToken.None));
    }

    [Fact]
    public async Task AuctionRealtimeWatchReturnsSnapshotAndPublishesOnlyAVersionHint()
    {
        await using var factory = Factory();
        using var organizer = Client(factory);
        var scenario = await AuctionRealtimePrepareSession(organizer);
        using var participant = Client(factory);
        var cookie = await AuctionRealtimeLoginCookie(participant, scenario.MemberEmail, Password);
        using var socket = await AuctionRealtimeConnect(factory, cookie, "http://localhost:6061");

        await AuctionRealtimeSend(socket, new
        {
            type = 1,
            invocationId = "watch",
            target = "WatchSession",
            arguments = new[] { scenario.SessionId.ToString() },
        });
        var completion = await AuctionRealtimeReceive(socket, message =>
            message.GetProperty("type").GetInt32() == 3 &&
            message.GetProperty("invocationId").GetString() == "watch");
        var snapshot = completion.GetProperty("result");
        Assert.Equal(scenario.SessionId, snapshot.GetProperty("id").GetGuid());
        var watchedVersion = snapshot.GetProperty("version").GetInt64();

        await AuctionRealtimeExecute(
            "UPDATE AuctionSessions SET Version = Version + 1 WHERE Id = @SessionId",
            new { scenario.SessionId });
        var changed = await AuctionRealtimeReceive(socket, message =>
            message.GetProperty("type").GetInt32() == 1 &&
            message.GetProperty("target").GetString() == "AuctionChanged");
        var hint = Assert.Single(changed.GetProperty("arguments").EnumerateArray());
        Assert.Equal(scenario.SessionId, hint.GetProperty("sessionId").GetGuid());
        Assert.Equal(watchedVersion + 1, hint.GetProperty("version").GetInt64());
        Assert.Equal(
            ["sessionId", "version"],
            hint.EnumerateObject().Select(property => property.Name).Order().ToArray());
    }

    [Fact]
    public async Task AuctionRealtimePresenceCountsUsersAndUpdatesAfterDisconnect()
    {
        await using var factory = Factory();
        using var organizer = Client(factory);
        var scenario = await AuctionRealtimePrepareSession(organizer);
        using var participant = Client(factory);
        var memberCookie = await AuctionRealtimeLoginCookie(participant, scenario.MemberEmail, Password);
        var adminCookie = await AuctionRealtimeLoginCookie(organizer, "admin@example.test", "Test-Admin-123!");
        using var member = await AuctionRealtimeConnect(factory, memberCookie, "http://localhost:6061");
        using var secondTab = await AuctionRealtimeConnect(factory, memberCookie, "http://localhost:6061");
        using var admin = await AuctionRealtimeConnect(factory, adminCookie, "http://localhost:6061");
        foreach (var socket in new[] { member, secondTab, admin })
        {
            await AuctionRealtimeSend(socket, new
            {
                type = 1,
                invocationId = "watch",
                target = "WatchSession",
                arguments = new[] { scenario.SessionId.ToString() },
            });
            await AuctionRealtimeReceive(socket, message =>
                message.GetProperty("type").GetInt32() == 3 &&
                message.GetProperty("invocationId").GetString() == "watch");
        }

        var presence = await AuctionRealtimeReceive(member, message =>
            message.GetProperty("type").GetInt32() == 1 &&
            message.GetProperty("target").GetString() == "AuctionPresenceChanged" &&
            message.GetProperty("arguments")[0].GetProperty("connectedUsers").GetInt32() == 2);
        Assert.Equal(scenario.SessionId, presence.GetProperty("arguments")[0].GetProperty("sessionId").GetGuid());

        await admin.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        var remaining = await AuctionRealtimeReceive(member, message =>
            message.GetProperty("type").GetInt32() == 1 &&
            message.GetProperty("target").GetString() == "AuctionPresenceChanged" &&
            message.GetProperty("arguments")[0].GetProperty("connectedUsers").GetInt32() == 1);
        Assert.Equal(scenario.SessionId, remaining.GetProperty("arguments")[0].GetProperty("sessionId").GetGuid());
    }

    [Fact]
    public async Task AuctionRealtimeObserverRevokesSubscriptionWhenMembershipIsNoLongerActive()
    {
        await using var factory = Factory();
        using var organizer = Client(factory);
        var scenario = await AuctionRealtimePrepareSession(organizer);
        using var participant = Client(factory);
        var cookie = await AuctionRealtimeLoginCookie(participant, scenario.MemberEmail, Password);
        using var socket = await AuctionRealtimeConnect(factory, cookie, "http://localhost:6061");
        await AuctionRealtimeSend(socket, new
        {
            type = 1,
            invocationId = "watch",
            target = "WatchSession",
            arguments = new[] { scenario.SessionId.ToString() },
        });
        await AuctionRealtimeReceive(socket, message =>
            message.GetProperty("type").GetInt32() == 3 &&
            message.GetProperty("invocationId").GetString() == "watch");

        await AuctionRealtimeExecute("""
            UPDATE LeagueMembers SET Status = 0 WHERE LeagueId = @LeagueId AND UserId = @MemberId;
            UPDATE AuctionSessions SET Version = Version + 1 WHERE Id = @SessionId;
            """, scenario);

        var registry = factory.Services.GetRequiredService<AuctionSubscriptionRegistry>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (registry.GetObservers().Any(observer => observer.SessionId == scenario.SessionId))
        {
            await Task.Delay(50, timeout.Token);
        }

        using var noMessage = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            AuctionRealtimeReceive(socket, _ => true, noMessage.Token));
    }

    [Fact]
    public async Task AuctionRealtimeWatchRevalidatesARevokedServerRoleAndReturnsAStableError()
    {
        await using var factory = Factory();
        using var organizer = Client(factory);
        var scenario = await AuctionRealtimePrepareSession(organizer);
        using var admin = Client(factory);
        var cookie = await AuctionRealtimeLoginCookie(admin, "admin@example.test", "Test-Admin-123!");
        using var socket = await AuctionRealtimeConnect(factory, cookie, "http://localhost:6061");

        await AuctionRealtimeExecute("""
            DELETE userRole
            FROM AspNetUserRoles userRole
            INNER JOIN AspNetRoles role ON role.Id = userRole.RoleId
            WHERE userRole.UserId = @AdminId AND role.NormalizedName = N'SUPERADMIN';
            UPDATE LeagueMembers SET Status = 0
            WHERE LeagueId = @LeagueId AND UserId = @AdminId;
            """, new
        {
            AdminId = fixture.Admin.UserId!.Value,
            scenario.LeagueId,
        });

        try
        {
            await AuctionRealtimeSend(socket, new
            {
                type = 1,
                invocationId = "revoked-role",
                target = "WatchSession",
                arguments = new[] { scenario.SessionId.ToString() },
            });
            var completion = await AuctionRealtimeReceive(socket, message =>
                message.GetProperty("type").GetInt32() == 3 &&
                message.GetProperty("invocationId").GetString() == "revoked-role");
            var error = completion.GetProperty("error").GetString();
            Assert.EndsWith("auth.forbidden", error, StringComparison.Ordinal);
        }
        finally
        {
            await AuctionRealtimeExecute("""
                IF NOT EXISTS
                (
                    SELECT 1 FROM AspNetUserRoles userRole
                    INNER JOIN AspNetRoles role ON role.Id = userRole.RoleId
                    WHERE userRole.UserId = @AdminId AND role.NormalizedName = N'SUPERADMIN'
                )
                INSERT INTO AspNetUserRoles (UserId, RoleId)
                SELECT @AdminId, Id FROM AspNetRoles WHERE NormalizedName = N'SUPERADMIN';
                UPDATE LeagueMembers SET Status = 1
                WHERE LeagueId = @LeagueId AND UserId = @AdminId;
                """, new
            {
                AdminId = fixture.Admin.UserId!.Value,
                scenario.LeagueId,
            });
        }
    }

    [Fact]
    public async Task AuctionRealtimeObservationBatchRevalidatesMembershipAndServerRoleFromSql()
    {
        await using var factory = Factory();
        using var organizer = Client(factory);
        var scenario = await AuctionRealtimePrepareSession(organizer);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        var query = new ObserveAuctionsQuery(db);
        var outsiderId = Guid.CreateVersion7();
        db.Users.Add(new ApplicationUser
        {
            Id = outsiderId,
            UserName = $"{outsiderId:N}@example.test",
            NormalizedUserName = $"{outsiderId:N}@EXAMPLE.TEST",
            Email = $"{outsiderId:N}@example.test",
            NormalizedEmail = $"{outsiderId:N}@EXAMPLE.TEST",
            DisplayName = "Outsider",
        });
        await db.SaveChangesAsync();

        var rows = await query.ExecuteAsync(
            [
                new("active", scenario.SessionId, scenario.MemberId),
                new("admin", scenario.SessionId, fixture.Admin.UserId!.Value),
                new("outsider", scenario.SessionId, outsiderId),
            ],
            CancellationToken.None);

        Assert.True(rows.Single(row => row.ConnectionId == "active").HasAccess);
        Assert.True(rows.Single(row => row.ConnectionId == "admin").HasAccess);
        Assert.False(rows.Single(row => row.ConnectionId == "outsider").HasAccess);
        Assert.All(rows, row => Assert.Equal(scenario.SessionId, row.SessionId));

        await db.Database.GetDbConnection().ExecuteAsync(
            "UPDATE LeagueMembers SET Status = 0 WHERE LeagueId = @LeagueId AND UserId = @MemberId",
            scenario);
        var revoked = await query.ExecuteAsync(
            [new("active", scenario.SessionId, scenario.MemberId)],
            CancellationToken.None);
        Assert.False(Assert.Single(revoked).HasAccess);
    }

    private async Task<AuctionRealtimeScenario> AuctionRealtimePrepareSession(HttpClient organizer)
    {
        await Login(organizer, "admin@example.test", "Test-Admin-123!");
        var setup = await PrepareAuctionLeague();
        var draft = await Data(await organizer.PostAsJsonAsync(
            "/api/Catalog/Imports",
            new { seasonName = setup.SeasonName, csv = CatalogCsv() }), HttpStatusCode.Created);
        var listId = draft.GetProperty("id").GetGuid();
        await Data(await organizer.PostAsJsonAsync(
            $"/api/Catalog/Versions/{listId}/Publish", new { }), HttpStatusCode.OK);
        await Data(await organizer.PutAsJsonAsync(
            $"/api/Leagues/{setup.LeagueId}/Seasons/{setup.SeasonId}/Catalog",
            new { listVersionId = listId }), HttpStatusCode.OK);
        var session = await Data(await organizer.PostAsJsonAsync(
            "/api/Auctions/Sessions",
            new
            {
                leagueId = setup.LeagueId,
                leagueSeasonId = setup.SeasonId,
                teamOrder = new[] { setup.FirstTeamId, setup.SecondTeamId },
            }), HttpStatusCode.Created);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        var memberId = await db.Users
            .Where(user => user.Email == setup.Email)
            .Select(user => user.Id)
            .SingleAsync();
        return new AuctionRealtimeScenario(
            session.GetProperty("id").GetGuid(),
            setup.LeagueId,
            memberId,
            setup.Email);
    }

    private static async Task<string> AuctionRealtimeLoginCookie(
        HttpClient client,
        string email,
        string password)
    {
        await RefreshCsrf(client);
        using var response = await client.PostAsJsonAsync("/api/Auth/Login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("Fantastiche.Auth=", StringComparison.Ordinal))
            .Split(';', 2)[0];
    }

    private static async Task<WebSocket> AuctionRealtimeConnect(
        WebApplicationFactory<Program> factory,
        string cookie,
        string? origin)
    {
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
        {
            request.Headers.Cookie = cookie;
            if (origin is not null)
            {
                request.Headers.Origin = origin;
            }
        };
        var socket = await client.ConnectAsync(
            new Uri("ws://localhost/hubs/Auctions"),
            CancellationToken.None);
        await AuctionRealtimeSendRaw(socket, "{\"protocol\":\"json\",\"version\":1}\u001e");
        var handshake = await AuctionRealtimeReceiveRaw(socket, CancellationToken.None);
        Assert.Equal("{}", handshake);
        return socket;
    }

    private static Task AuctionRealtimeSend(WebSocket socket, object message)
        => AuctionRealtimeSendRaw(socket, JsonSerializer.Serialize(message) + '\u001e');

    private static Task AuctionRealtimeSendRaw(WebSocket socket, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }

    private static async Task<JsonElement> AuctionRealtimeReceive(
        WebSocket socket,
        Func<JsonElement, bool> predicate,
        CancellationToken cancellationToken = default)
    {
        using var timeout = cancellationToken == default
            ? new CancellationTokenSource(TimeSpan.FromSeconds(10))
            : null;
        var token = cancellationToken == default ? timeout!.Token : cancellationToken;
        while (true)
        {
            var json = JsonDocument.Parse(await AuctionRealtimeReceiveRaw(socket, token)).RootElement.Clone();
            if (predicate(json))
            {
                return json;
            }
        }
    }

    private static async Task<string> AuctionRealtimeReceiveRaw(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var content = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("La connessione SignalR è stata chiusa.");
            }

            content.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(content.ToArray()).TrimEnd('\u001e');
    }

    private async Task AuctionRealtimeExecute(string sql, object parameters)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
        await db.Database.GetDbConnection().ExecuteAsync(sql, parameters);
    }

    private sealed record AuctionRealtimeScenario(
        Guid SessionId,
        Guid LeagueId,
        Guid MemberId,
        string MemberEmail);
}
