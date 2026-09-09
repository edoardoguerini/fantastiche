using Fantastiche.Infrastructure.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Fantastiche.Scheduler.Jobs;

public sealed class EmailDispatchJob(IServiceScopeFactory scopes, ILogger<EmailDispatchJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                for (var i = 0; i < 20 && !stoppingToken.IsCancellationRequested; i++)
                {
                    await using var scope = scopes.CreateAsyncScope();
                    if (!await scope.ServiceProvider.GetRequiredService<EmailDispatcher>().DispatchOneAsync(stoppingToken)) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogError("Ciclo email non riuscito; il prossimo ciclo riproverà."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
