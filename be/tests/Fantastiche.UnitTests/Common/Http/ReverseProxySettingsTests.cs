using System.Net;
using Fantastiche.Application.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fantastiche.UnitTests.Common.Http;

public sealed class ReverseProxySettingsTests
{
    private static IConfiguration Build(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void Apply_SenzaConfigurazione_MantieneLoopbackEUnSoloHop()
    {
        var options = new ForwardedHeadersOptions();

        ReverseProxySettings.Apply(options, Build());

        Assert.Equal(1, options.ForwardLimit);
        Assert.Contains(IPAddress.IPv6Loopback, options.KnownProxies);
        Assert.NotEmpty(options.KnownIPNetworks);
    }

    [Fact]
    public void Apply_ConKnownProxies_SostituisceIDefault()
    {
        var options = new ForwardedHeadersOptions();

        ReverseProxySettings.Apply(options, Build(("ReverseProxy:KnownProxies:0", "10.1.2.3")));

        Assert.Equal([IPAddress.Parse("10.1.2.3")], options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    public void Apply_ConKnownProxyNonValido_Fallisce()
    {
        var options = new ForwardedHeadersOptions();

        var exception = Assert.Throws<InvalidOperationException>(
            () => ReverseProxySettings.Apply(options, Build(("ReverseProxy:KnownProxies:0", "non-un-ip"))));

        Assert.Contains("non-un-ip", exception.Message);
    }

    [Fact]
    public void Apply_ConTrustAllProxies_SvuotaProxyERetiConosciute()
    {
        var options = new ForwardedHeadersOptions();

        ReverseProxySettings.Apply(options, Build(("ReverseProxy:TrustAllProxies", "true")));

        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
    }

    [Fact]
    public void Apply_ConForwardLimit_ImpostaGliHop()
    {
        var options = new ForwardedHeadersOptions();

        ReverseProxySettings.Apply(options, Build(("ReverseProxy:ForwardLimit", "3")));

        Assert.Equal(3, options.ForwardLimit);
    }

    [Fact]
    public void Apply_ConForwardLimitNonValido_Fallisce()
    {
        var options = new ForwardedHeadersOptions();

        Assert.Throws<InvalidOperationException>(
            () => ReverseProxySettings.Apply(options, Build(("ReverseProxy:ForwardLimit", "0"))));
    }

    [Fact]
    public void Apply_ConTrustAllProxiesEKnownProxiesInsieme_Fallisce()
    {
        var options = new ForwardedHeadersOptions();

        Assert.Throws<InvalidOperationException>(() => ReverseProxySettings.Apply(
            options,
            Build(("ReverseProxy:TrustAllProxies", "true"), ("ReverseProxy:KnownProxies:0", "10.1.2.3"))));
    }

    [Fact]
    public void Apply_ConTrustAllProxiesNonBooleano_Fallisce()
    {
        var options = new ForwardedHeadersOptions();

        Assert.Throws<InvalidOperationException>(
            () => ReverseProxySettings.Apply(options, Build(("ReverseProxy:TrustAllProxies", "yes"))));
    }

    // Configurazione di produzione: TrustAllProxies e limite 2, con nginx che riscrive
    // X-Forwarded-For con la sola voce fidata. Il middleware consuma le voci da destra.
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]                 // solo nginx a monte
    [InlineData("203.0.113.7, 10.0.0.6", "203.0.113.7")]       // nginx + ingress dell'API
    [InlineData("1.2.3.4, 203.0.113.7, 10.0.0.6", "203.0.113.7")] // voce spuria oltre il limite: ignorata
    public async Task Middleware_InProduzione_RisaleAlClientEntroDueHop(string forwardedFor, string expectedClient)
    {
        var remoteIp = await ResolveRemoteIpAsync(
            Build(("ReverseProxy:TrustAllProxies", "true"), ("ReverseProxy:ForwardLimit", "2")),
            forwardedFor);

        Assert.Equal(IPAddress.Parse(expectedClient), remoteIp);
    }

    [Fact]
    public async Task Middleware_InProduzione_ConCatenaCortaLaVoceDelClientEntraNelLimite()
    {
        // Documenta il limite del meccanismo: se il proxy a monte NON riscrivesse l'header,
        // con catena di una voce reale una voce aggiunta dal client verrebbe accettata.
        var remoteIp = await ResolveRemoteIpAsync(
            Build(("ReverseProxy:TrustAllProxies", "true"), ("ReverseProxy:ForwardLimit", "2")),
            "1.2.3.4, 203.0.113.7");

        Assert.Equal(IPAddress.Parse("1.2.3.4"), remoteIp);
    }

    [Fact]
    public async Task Middleware_ConDefault_IgnoraGliHeaderDaProxyNonLoopback()
    {
        var remoteIp = await ResolveRemoteIpAsync(Build(), "203.0.113.7");

        Assert.Equal(IPAddress.Parse("10.0.0.6"), remoteIp);
    }

    private static async Task<IPAddress?> ResolveRemoteIpAsync(IConfiguration configuration, string forwardedFor)
    {
        var options = new ForwardedHeadersOptions();
        ReverseProxySettings.Apply(options, configuration);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.6");
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options));

        await middleware.Invoke(context);

        return context.Connection.RemoteIpAddress;
    }
}
