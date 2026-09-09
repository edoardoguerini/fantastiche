using Fantastiche.Application.Hubs;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Fantastiche.Application.Infrastructure.Auctions;

public sealed record AuctionChanged(Guid SessionId, long Version);

public sealed class AuctionObserverWorker(
    AuctionSubscriptionRegistry subscriptions,
    IServiceScopeFactory scopes,
    IHubContext<AuctionsHub> hub,
    IOptions<AuctionRealtimeOptions> options,
    ILogger<AuctionObserverWorker> logger) : BackgroundService
{
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
                continue;
            }

            if (!subscriptions.NeedsNotification(
                    observation.ConnectionId,
                    observation.SessionId,
                    observation.Version))
            {
                continue;
            }

            try
            {
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
