using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Fantastiche.Application.Infrastructure.Http;

/// <summary>
/// Configura la fiducia negli header X-Forwarded-* letti da <c>ReverseProxy:*</c>.
/// Default: solo loopback e un hop, come ASP.NET Core. Dietro un ingress interno
/// (Container Apps) l'API accetta gli header da qualsiasi hop con <c>TrustAllProxies</c>
/// e risale esattamente <c>ForwardLimit</c> hop per ottenere l'IP reale del client.
/// </summary>
public static class ReverseProxySettings
{
    public static void Apply(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = ReadForwardLimit(configuration);

        if (configuration.GetValue<bool>("ReverseProxy:TrustAllProxies"))
        {
            // Liste vuote = nessun controllo sull'hop: valido solo se l'API non è raggiungibile
            // direttamente da Internet, altrimenti chiunque potrebbe falsificare gli header.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
            return;
        }

        var configuredProxies = configuration
            .GetSection("ReverseProxy:KnownProxies")
            .Get<string[]>() ?? [];
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
