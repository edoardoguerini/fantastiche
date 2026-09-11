using System.Net;
using Fantastiche.Application.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

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
}
