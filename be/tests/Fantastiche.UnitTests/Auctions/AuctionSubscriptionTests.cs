using Fantastiche.Application.Infrastructure.Auctions;
using Fantastiche.Application.Hubs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Fantastiche.UnitTests.Auctions;

public sealed class AuctionSubscriptionTests
{
    [Fact]
    public void PresenceCountsDistinctUsersInTheSessionAndRemovesDisconnectedUsers()
    {
        var registry = new AuctionSubscriptionRegistry();
        var sessionId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        registry.TryWatch("tab-1", userId, sessionId, 1);
        registry.TryWatch("tab-2", userId, sessionId, 1);
        registry.TryWatch("other-user", Guid.CreateVersion7(), sessionId, 1);
        registry.TryWatch("other-room", Guid.CreateVersion7(), Guid.CreateVersion7(), 1);
        Assert.Equal(2, registry.CountConnectedUsers(sessionId));
        registry.RemoveConnection("tab-1");
        Assert.Equal(2, registry.CountConnectedUsers(sessionId));
        registry.Unwatch("tab-2", sessionId);
        Assert.Equal(1, registry.CountConnectedUsers(sessionId));
        registry.RemoveConnection("other-user");
        Assert.Equal(0, registry.CountConnectedUsers(sessionId));
    }

    [Fact]
    public void WatchLimitsEachConnectionToSixteenDistinctSessions()
    {
        var registry = new AuctionSubscriptionRegistry();
        var userId = Guid.CreateVersion7();
        var connectionId = "connection-1";
        var sessions = Enumerable.Range(0, AuctionSubscriptionRegistry.MaxSessionsPerConnection)
            .Select(_ => Guid.CreateVersion7())
            .ToArray();

        foreach (var sessionId in sessions)
        {
            Assert.True(registry.TryWatch(connectionId, userId, sessionId, 1));
        }

        Assert.True(registry.TryWatch(connectionId, userId, sessions[0], 2));
        Assert.False(registry.TryWatch(connectionId, userId, Guid.CreateVersion7(), 1));
        Assert.Equal(AuctionSubscriptionRegistry.MaxSessionsPerConnection, registry.GetObservers().Count);
    }

    [Fact]
    public void DisconnectRemovesEverySessionOwnedByTheConnection()
    {
        var registry = new AuctionSubscriptionRegistry();
        var userId = Guid.CreateVersion7();
        Assert.True(registry.TryWatch("connection-1", userId, Guid.CreateVersion7(), 1));
        Assert.True(registry.TryWatch("connection-1", userId, Guid.CreateVersion7(), 1));
        Assert.True(registry.TryWatch("connection-2", userId, Guid.CreateVersion7(), 1));

        registry.RemoveConnection("connection-1");

        var observer = Assert.Single(registry.GetObservers());
        Assert.Equal("connection-2", observer.ConnectionId);
    }

    [Fact]
    public void AdvanceVersionRequiresAnExistingSubscriptionAndANewerVersion()
    {
        var registry = new AuctionSubscriptionRegistry();
        var sessionId = Guid.CreateVersion7();
        Assert.True(registry.TryWatch("connection-1", Guid.CreateVersion7(), sessionId, 4));

        Assert.False(registry.TryAdvanceVersion("connection-1", sessionId, 4));
        Assert.False(registry.TryAdvanceVersion("connection-1", sessionId, 3));
        Assert.True(registry.TryAdvanceVersion("connection-1", sessionId, 5));
        Assert.False(registry.TryAdvanceVersion("connection-2", sessionId, 6));
        Assert.Equal(5, Assert.Single(registry.GetObservers()).KnownVersion);
    }

    [Fact]
    public void FailedDeliveryRemainsPendingUntilASuccessAdvancesTheVersion()
    {
        var registry = new AuctionSubscriptionRegistry();
        var sessionId = Guid.CreateVersion7();
        Assert.True(registry.TryWatch("connection-1", Guid.CreateVersion7(), sessionId, 4));

        Assert.True(registry.NeedsNotification("connection-1", sessionId, 5));
        Assert.True(registry.NeedsNotification("connection-1", sessionId, 5));

        Assert.True(registry.TryAdvanceVersion("connection-1", sessionId, 5));
        Assert.False(registry.NeedsNotification("connection-1", sessionId, 5));
    }
}

public sealed class AuctionOriginMiddlewareTests
{
    [Fact]
    public async Task HubRequestFromAnUnlistedBrowserOriginIsRejected()
    {
        var called = false;
        var middleware = new AuctionOriginMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            Options.Create(new AuctionRealtimeOptions
            {
                AllowedOrigins = ["http://localhost:6061"],
            }));
        var context = new DefaultHttpContext();
        context.Request.Path = AuctionsHub.Route;
        context.Request.Headers.Origin = "https://untrusted.example";

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task HubRequestWithoutOriginIsAllowedForNativeAuthenticatedClients()
    {
        var called = false;
        var middleware = new AuctionOriginMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            Options.Create(new AuctionRealtimeOptions()));
        var context = new DefaultHttpContext();
        context.Request.Path = AuctionsHub.Route;

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public void ConfiguredOriginsNormalizeTrailingSlashAndDefaultPort()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "http://localhost:80/",
            }).Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var services = new ServiceCollection();

        services.AddAuctionRealtime(configuration, environment);
        using var provider = services.BuildServiceProvider();

        Assert.Equal(
            ["http://localhost"],
            provider.GetRequiredService<IOptions<AuctionRealtimeOptions>>().Value.AllowedOrigins);
    }
}
