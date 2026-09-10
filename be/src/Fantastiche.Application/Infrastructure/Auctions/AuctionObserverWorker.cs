using Fantastiche.Application.Hubs;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Fantastiche.Application.Infrastructure.Auctions;

public sealed record AuctionChanged(Guid SessionId, long Version);
public sealed record AuctionPresenceChanged(Guid SessionId, int ConnectedUsers, IReadOnlyList<AuctionParticipantView> Users);

public sealed class AuctionObserverWorker(
    AuctionSubscriptionRegistry subscriptions,
    IServiceScopeFactory scopes,
    IHubContext<AuctionsHub> hub,
    IOptions<AuctionRealtimeOptions> options,
    ILogger<AuctionObserverWorker> logger) : BackgroundService
{
    private readonly Dictionary<(string ConnectionId, Guid SessionId), AuctionParticipantView[]> knownPresence = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ObserveAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Controllo delle versioni d'asta non riuscito.");
            }

            await Task.Delay(options.Value.PollInterval, stoppingToken);
        }
    }

    private async Task ObserveAsync(CancellationToken cancellationToken)
    {
        var observers = subscriptions.GetObservers();
        var activeSubscriptions = observers.Select(observer => (observer.ConnectionId, observer.SessionId)).ToHashSet();
        foreach (var key in knownPresence.Keys.Where(key => !activeSubscriptions.Contains(key)).ToArray())
        {
            knownPresence.Remove(key);
        }
        if (observers.Count == 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<ObserveAuctionsQuery>();
        var observations = await query.ExecuteAsync(
            observers.Select(observer => new AuctionObservationRequest(
                observer.ConnectionId,
                observer.SessionId,
                observer.UserId)).ToArray(),
            cancellationToken);

        foreach (var observation in observations)
        {
            if (!observation.HasAccess)
            {
                subscriptions.Unwatch(observation.ConnectionId, observation.SessionId);
                knownPresence.Remove((observation.ConnectionId, observation.SessionId));
            }
        }

        var presence = observations.Where(observation => observation.HasAccess)
            .GroupBy(observation => observation.SessionId)
            .ToDictionary(group => group.Key, group => group
                .DistinctBy(observation => observation.UserId)
                .OrderBy(observation => observation.UserId)
                .Select(observation => new AuctionParticipantView(
                    observation.UserId, observation.DisplayName, observation.TeamName, observation.IsOrganizer))
                .ToArray());
        foreach (var observation in observations.Where(observation => observation.HasAccess))
        {
            try
            {
                var key = (observation.ConnectionId, observation.SessionId);
                var users = presence[observation.SessionId];
                if (!knownPresence.TryGetValue(key, out var previousUsers) || !users.SequenceEqual(previousUsers))
                {
                    await hub.Clients.Client(observation.ConnectionId).SendAsync(
                        "AuctionPresenceChanged",
                        new AuctionPresenceChanged(observation.SessionId, users.Length, users),
                        cancellationToken);
                    knownPresence[key] = users;
                }

                if (!subscriptions.NeedsNotification(
                        observation.ConnectionId,
                        observation.SessionId,
                        observation.Version))
                {
                    continue;
                }

                await hub.Clients.Client(observation.ConnectionId).SendAsync(
                    "AuctionChanged",
                    new AuctionChanged(observation.SessionId, observation.Version),
                    cancellationToken);
                subscriptions.TryAdvanceVersion(
                    observation.ConnectionId,
                    observation.SessionId,
                    observation.Version);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Notifica asta non riuscita per connessione {ConnectionId} e sessione {SessionId}.",
                    observation.ConnectionId,
                    observation.SessionId);
            }
        }
    }
}
