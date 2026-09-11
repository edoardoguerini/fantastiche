using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Fantastiche.Application.Infrastructure.Http;

/// <summary>
/// Configura la fiducia negli header X-Forwarded-* letti da <c>ReverseProxy:*</c>.
/// Default: solo loopback e un hop, come ASP.NET Core. Dietro un ingress interno
/// (Container Apps) l'API accetta gli header da qualsiasi hop con <c>TrustAllProxies</c>
/// e risale al più <c>ForwardLimit</c> voci di X-Forwarded-For partendo da destra: il
/// proxy a monte deve riscrivere l'header con le sole voci fidate, altrimenti una catena
/// più corta del limite lascerebbe entrare voci aggiunte dal client.
/// </summary>
public static class ReverseProxySettings
{
    /// <summary>Applica la configurazione; fallisce subito su valori non validi o contraddittori.</summary>
    public static void Apply(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = ReadForwardLimit(configuration);

        var trustAllProxies = ReadTrustAllProxies(configuration);
        var configuredProxies = configuration
            .GetSection("ReverseProxy:KnownProxies")
            .Get<string[]>() ?? [];

        if (trustAllProxies && configuredProxies.Length > 0)
        {
            throw new InvalidOperationException(
                "ReverseProxy:TrustAllProxies e ReverseProxy:KnownProxies si escludono: scegliere uno solo dei due.");
        }

        if (trustAllProxies)
        {
            // Liste vuote = nessun controllo sull'hop: valido solo se l'API non è raggiungibile
            // direttamente da Internet, altrimenti chiunque potrebbe falsificare gli header.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            return;
        }

        if (configuredProxies.Length == 0)
        {
            // I default loopback restano attivi: non accettiamo header da proxy arbitrari.
            return;
        }

        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        foreach (var configuredProxy in configuredProxies)
        {
            if (!IPAddress.TryParse(configuredProxy, out var address))
            {
                throw new InvalidOperationException(
                    $"ReverseProxy:KnownProxies contiene un indirizzo non valido: {configuredProxy}.");
            }

            options.KnownProxies.Add(address);
        }
    }

    /// <summary>
    /// Valida la configurazione all'avvio: <c>Configure&lt;T&gt;</c> è pigro e un errore
    /// emergerebbe solo alla prima richiesta, prima dell'exception handler.
    /// </summary>
    public static void Validate(IConfiguration configuration) => Apply(new ForwardedHeadersOptions(), configuration);

    private static bool ReadTrustAllProxies(IConfiguration configuration)
    {
        var raw = configuration["ReverseProxy:TrustAllProxies"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!bool.TryParse(raw, out var value))
        {
            throw new InvalidOperationException(
                $"ReverseProxy:TrustAllProxies deve essere true o false, non '{raw}'.");
        }

        return value;
    }

    private static int ReadForwardLimit(IConfiguration configuration)
    {
        var raw = configuration["ReverseProxy:ForwardLimit"];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 1;
        }

        if (!int.TryParse(raw, out var limit) || limit < 1)
        {
            throw new InvalidOperationException(
                $"ReverseProxy:ForwardLimit deve essere un intero maggiore di zero, non '{raw}'.");
        }

        return limit;
    }
}
