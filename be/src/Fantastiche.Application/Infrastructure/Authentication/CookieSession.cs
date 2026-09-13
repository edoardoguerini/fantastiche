using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

namespace Fantastiche.Application.Infrastructure.Authentication;

public static class CookieSession
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan MaximumLifetime = TimeSpan.FromDays(30);
    private const string StartedAtKey = "Fantastiche.SessionStartedAt";

    public static Task SigningInAsync(CookieSigningInContext context)
    {
        // Le proprietà restano nel ticket protetto anche quando Identity rinnova le claim.
        context.Properties.Items.TryAdd(StartedAtKey,
            context.Options.TimeProvider!.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        return Task.CompletedTask;
    }

    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var now = context.Options.TimeProvider!.GetUtcNow();
        var legacySession = !context.Properties.Items.TryGetValue(StartedAtKey, out var value);
        // I cookie precedenti usano la propria emissione come origine, senza azzerare il limite.
        var startedAt = legacySession ? context.Properties.IssuedUtc
            : DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed) ? parsed : (DateTimeOffset?)null;
        if (startedAt is null || startedAt > now || now - startedAt.Value >= MaximumLifetime)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }

        context.Properties.Items[StartedAtKey] = startedAt.Value.ToString("O", CultureInfo.InvariantCulture);
        // Conservare la verifica Identity: una sessione revocata non può essere rinnovata.
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal is null) return;

        var absoluteExpiry = startedAt.Value + MaximumLifetime;
        var slidingRenewal = now - context.Properties.IssuedUtc > context.Properties.ExpiresUtc - now;
        if (legacySession || context.ShouldRenew || slidingRenewal || context.Properties.ExpiresUtc > absoluteExpiry)
        {
            // Anche il ticket usato da SignalR deve scadere entro il limite assoluto.
            context.ShouldRenew = true;
            context.Properties.IssuedUtc = now;
            context.Properties.ExpiresUtc = now + Lifetime < absoluteExpiry ? now + Lifetime : absoluteExpiry;
        }
    }
}
