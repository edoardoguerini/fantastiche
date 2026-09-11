using System.Reflection;
using System.Threading.RateLimiting;
using Fantastiche.Application.Infrastructure.Auctions;
using Fantastiche.Application.Infrastructure.Authentication;
using Fantastiche.Application.Infrastructure.Http;
using Fantastiche.Application.Infrastructure.Middleware;
using Fantastiche.Application.Infrastructure.Modules;
using Fantastiche.Infrastructure;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddFantasticheInfrastructure(builder.Configuration);
builder.Services.AddAuctionRealtime(builder.Configuration, builder.Environment);
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
builder.Services.AddFantasticheModules(Assembly.GetExecutingAssembly());
builder.Services.AddScoped<SuperAdminBootstrapper>();
builder.Services.AddAuthorization(options =>
    options.AddPolicy(FantasticheRoles.SuperAdmin, policy => policy.RequireRole(FantasticheRoles.SuperAdmin)));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FantasticheDbContext>("database", tags: ["ready"]);

ReverseProxySettings.Validate(builder.Configuration);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    ReverseProxySettings.Apply(options, builder.Configuration));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "Fantastiche.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = context => ApiResults.WriteErrorAsync(
        context.HttpContext,
        StatusCodes.Status401Unauthorized,
        "auth.required",
        "Accesso richiesto.");
    options.Events.OnRedirectToAccessDenied = context => ApiResults.WriteErrorAsync(
        context.HttpContext,
        StatusCodes.Status403Forbidden,
        "auth.forbidden",
        "Operazione non consentita.");
});
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "Fantastiche.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    options.OnRejected = (context, cancellationToken) => new ValueTask(
        ApiResults.WriteErrorAsync(
            context.HttpContext,
            StatusCodes.Status429TooManyRequests,
            "auth.rate_limited",
            "Troppi tentativi. Riprova più tardi.",
            cancellationToken));
});

var app = builder.Build();

var mediaImportIndex = Array.IndexOf(args, "--import-player-media");
if (mediaImportIndex >= 0)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PlayerMediaImport");
    try
    {
        if (mediaImportIndex + 1 >= args.Length || args[mediaImportIndex + 1].StartsWith("--", StringComparison.Ordinal))
            throw new InvalidOperationException("Specifica il percorso del manifest dopo --import-player-media.");
        var result = await scope.ServiceProvider.GetRequiredService<PlayerMediaImporter>().ImportAsync(args[mediaImportIndex + 1]);
        logger.LogInformation("Immagini: {Inserted} inserite, {Updated} aggiornate, {Unchanged} invariate, {Skipped} scartate.",
            result.Inserted, result.Updated, result.Unchanged, result.Skipped);
    }
    catch (Exception exception)
    {
        logger.LogError("Importazione metadati immagini non riuscita: {Message}", exception.Message);
        Environment.ExitCode = 1;
    }
    return;
}

var clubMediaImportIndex = Array.IndexOf(args, "--import-club-media");
if (clubMediaImportIndex >= 0)
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ClubMediaImport");
    try
    {
        if (clubMediaImportIndex + 1 >= args.Length || args[clubMediaImportIndex + 1].StartsWith("--", StringComparison.Ordinal))
            throw new InvalidOperationException("Specifica il percorso del manifest dopo --import-club-media.");
        var result = await scope.ServiceProvider.GetRequiredService<ClubMediaImporter>().ImportAsync(args[clubMediaImportIndex + 1]);
        logger.LogInformation("Loghi club: {Inserted} inserite, {Updated} aggiornate, {Unchanged} invariate, {Skipped} scartate.",
            result.Inserted, result.Updated, result.Unchanged, result.Skipped);
    }
    catch (Exception exception)
    {
        logger.LogError("Importazione metadati loghi club non riuscita: {Message}", exception.Message);
        Environment.ExitCode = 1;
    }
    return;
}

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<FantasticheDbContext>()
        .Database.MigrateAsync();
    return;
}

if (args.Contains("--bootstrap-superadmin", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Bootstrap");
    try
    {
        var user = await scope.ServiceProvider.GetRequiredService<SuperAdminBootstrapper>()
            .BootstrapAsync();
        logger.LogInformation("Creato il SuperAdmin {UserId}.", user.Id);
    }
    catch (Exception exception)
    {
        logger.LogError("Bootstrap SuperAdmin non riuscito: {Message}", exception.Message);
        Environment.ExitCode = 1;
    }

    return;
}

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    if (response.HasStarted || response.ContentLength is > 0)
    {
        return;
    }

    var (code, message) = response.StatusCode switch
    {
        StatusCodes.Status401Unauthorized => ("auth.required", "Accesso richiesto."),
        StatusCodes.Status403Forbidden => ("auth.forbidden", "Operazione non consentita."),
        StatusCodes.Status404NotFound => ("resource.not_found", "Risorsa non disponibile."),
        _ => ("http.error", "La richiesta non può essere completata."),
    };
    await ApiResults.WriteErrorAsync(
        statusCodeContext.HttpContext,
        response.StatusCode,
        code,
        message);
});
app.UseRateLimiter();
app.UseAuctionRealtime();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ExplicitAntiforgeryMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options
        .WithTitle("Fantastiche API")
        .WithTheme(ScalarTheme.Purple));
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});
app.MapFantasticheModules();
app.MapAuctionRealtime();

app.Run();

public partial class Program;
