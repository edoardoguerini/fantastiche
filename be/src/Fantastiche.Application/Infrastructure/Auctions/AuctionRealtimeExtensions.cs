using Fantastiche.Application.Hubs;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fantastiche.Application.Infrastructure.Auctions;

public static class AuctionRealtimeExtensions
{
    public static IServiceCollection AddAuctionRealtime(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = GetAllowedOrigins(configuration, environment);
        var pollMilliseconds = configuration.GetValue<int?>("Auctions:RealtimePollMilliseconds") ?? 500;
        if (pollMilliseconds <= 0)
        {
            throw new InvalidOperationException("Auctions:RealtimePollMilliseconds deve essere positivo.");
        }

        services.Configure<AuctionRealtimeOptions>(options =>
        {
            options.AllowedOrigins = allowedOrigins;
            options.PollInterval = TimeSpan.FromMilliseconds(pollMilliseconds);
        });
        services.AddCors(options => options.AddPolicy(
            AuctionRealtimeOptions.CorsPolicyName,
            policy => ConfigureCors(policy, allowedOrigins)));
        services.AddSignalR();
        services.TryAddSingleton<AuctionSubscriptionRegistry>();
        services.AddScoped<ObserveAuctionsQuery>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, AuctionObserverWorker>());
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, AuctionExpirationWorker>());
        return services;
    }

    public static IApplicationBuilder UseAuctionRealtime(this IApplicationBuilder app)
    {
        app.UseMiddleware<AuctionOriginMiddleware>();
        return app.UseCors(AuctionRealtimeOptions.CorsPolicyName);
    }

    public static IEndpointConventionBuilder MapAuctionRealtime(this IEndpointRouteBuilder endpoints)
    {
        var hub = endpoints.MapHub<AuctionsHub>(AuctionsHub.Route, options =>
        {
            options.CloseOnAuthenticationExpiration = true;
        });
        hub.RequireCors(AuctionRealtimeOptions.CorsPolicyName);
        hub.RequireAuthorization();
        return hub;
    }

    private static void ConfigureCors(CorsPolicyBuilder policy, string[] allowedOrigins)
    {
        if (allowedOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => false);
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    }

    private static string[] GetAllowedOrigins(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configured = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var candidates = configured.Length == 0 && environment.IsDevelopment()
            ? ["http://localhost:6060", "http://localhost:6061"]
            : configured;
        var origins = candidates
            .Select(origin => NormalizeOrigin(origin.Trim()))
            .Where(origin => origin.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var origin in origins)
        {
            if (origin.Contains('*', StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins contiene un'origine non valida: {origin}.");
            }
        }

        return origins;
    }

    private static string NormalizeOrigin(string origin)
    {
        if (origin.Length == 0)
        {
            return origin;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            uri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException(
                $"Cors:AllowedOrigins contiene un'origine non valida: {origin}.");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
