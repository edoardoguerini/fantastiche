using Fantastiche.Infrastructure.Auctions;
using Microsoft.Extensions.Options;

namespace Fantastiche.Application.Infrastructure.Auctions;

public sealed class AuctionExpirationWorker(
    IServiceScopeFactory scopes,
    IOptions<AuctionRealtimeOptions> options,
    ILogger<AuctionExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AuctionEngine>()
                    .CloseExpiredAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<AuctionEngine>()
                    .AdvanceBombsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Recupero delle aste scadute non riuscito.");
            }

            await Task.Delay(options.Value.PollInterval, stoppingToken);
        }
    }
}
