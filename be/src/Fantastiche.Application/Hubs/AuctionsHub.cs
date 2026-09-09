using System.Security.Claims;
using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Fantastiche.Application.Hubs;

[Authorize]
public sealed class AuctionsHub(
    IRequestPublisher publisher,
    Infrastructure.Auctions.AuctionSubscriptionRegistry subscriptions) : Hub
{
    public const string Route = "/hubs/Auctions";

    public async Task<AuctionSessionView> WatchSession(Guid sessionId)
    {
        var requestContext = CreateRequestContext();
        var snapshot = await GetSnapshotAsync(requestContext, sessionId);

        if (!subscriptions.TryWatch(
                Context.ConnectionId,
                requestContext.UserId!.Value,
                sessionId,
                snapshot.Version))
        {
            throw new HubException("auction.watch_limit");
        }

        try
        {
            var latest = await GetSnapshotAsync(requestContext, sessionId);
            subscriptions.TryWatch(
                Context.ConnectionId,
                requestContext.UserId.Value,
                sessionId,
                latest.Version);
            return latest;
        }
        catch
        {
            subscriptions.Unwatch(Context.ConnectionId, sessionId);
            throw;
        }
    }

    public void UnwatchSession(Guid sessionId)
        => subscriptions.Unwatch(Context.ConnectionId, sessionId);

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        subscriptions.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private RequestContext CreateRequestContext()
    {
        var identifier = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(identifier, out var userId))
        {
            throw new HubException("auth.required");
        }

        return new RequestContext(
            userId,
            Context.User!.IsInRole(FantasticheRoles.SuperAdmin),
            Context.GetHttpContext()?.TraceIdentifier ?? Context.ConnectionId);
    }

    private async Task<AuctionSessionView> GetSnapshotAsync(
        RequestContext requestContext,
        Guid sessionId)
    {
        try
        {
            return await publisher.QueryAsync<GetAuctionStateQuery, AuctionSessionView>(
                new GetAuctionStateQuery(requestContext, sessionId),
                Context.ConnectionAborted);
        }
        catch (DomainException exception)
        {
            throw new HubException(exception.Code);
        }
    }
}
