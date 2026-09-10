using Fantastiche.Infrastructure.Auctions;

namespace Fantastiche.IntegrationTests.Auctions;

public sealed partial class AuctionEngineTests
{
    [Fact]
    public async Task SessionCatalogExcludesTheOpenCallThenPreservesTheSeasonPurchaseAcrossSessions()
    {
        var data = await Seed();
        var session = await Create(data);
        var call = await Start(data, session.Id);
        var open = await Send<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(data.Users[0], session.Id));
        Assert.Equal(3, open.Total);
        Assert.DoesNotContain(open.Items, x => x.PlayerId == data.Players[0]);
        var all = await Send<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(data.Users[0], session.Id, AvailableOnly: false));
        var called = Assert.Single(all.Items, x => x.PlayerId == data.Players[0]);
        Assert.False(called.IsAvailable);
        Assert.Null(called.TeamId);
        await Expire(call.AuctionId!.Value);
        Assert.True(await Close(call.AuctionId.Value));
        var completed = await Send<ControlAuctionSessionCommand, AuctionCommandResult>(new(data.Users[0], session.Id, Guid.NewGuid(), "Complete"));
        Assert.True(completed.Accepted);
        var next = await Create(data);
        var nextCall = await Start(data, next.Id, player: 2);
        Assert.True(nextCall.Accepted);
        var historical = await Send<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(data.Users[0], session.Id));
        Assert.Equal(2, historical.Total);
        Assert.DoesNotContain(historical.Items, x => x.PlayerId == data.Players[0] || x.PlayerId == data.Players[2]);
        var purchases = await Send<GetAuctionCatalogQuery, AuctionPage<AuctionCatalogPlayerView>>(new(data.Users[0], next.Id, AvailableOnly: false, Role: "P"));
        Assert.Equal(data.Teams[0], Assert.Single(purchases.Items, x => !x.IsAvailable).TeamId);
    }
}
