using System.Net;
using System.Net.Http.Json;
using System.Globalization;
using Fantastiche.Infrastructure.Common.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Fantastiche.IntegrationTests.Http;

public sealed partial class HttpFlowTests
{
    [Fact]
    public async Task LoginCreatesPersistentSessionAndActivityRenewsIt()
    {
        var clock = new SessionClock();
        await using var factory = Factory(clock);
        using var client = Client(factory);
        await RefreshCsrf(client);
        using var login = await client.PostAsJsonAsync("/api/Auth/Login",
            new { email = "admin@example.test", password = "Test-Admin-123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var original = ReadSessionCookie(login);
        Assert.True(original.HttpOnly);
        Assert.True(original.Secure);
        Assert.Equal(clock.GetUtcNow().AddDays(7), original.Expires);

        clock.Advance(TimeSpan.FromDays(6));
        using var renewed = await client.GetAsync("/api/Auth/Me");
        Assert.Equal(HttpStatusCode.OK, renewed.StatusCode);
        Assert.Equal(clock.GetUtcNow().AddDays(7), ReadSessionCookie(renewed).Expires);

        // La PWA riaperta usa il cookie rinnovato anche oltre la prima scadenza.
        clock.Advance(TimeSpan.FromDays(2));
        using var reopened = Client(factory);
        var renewedCookie = ReadSessionCookie(renewed);
        reopened.DefaultRequestHeaders.Add("Cookie", $"{renewedCookie.Name}={renewedCookie.Value}");
        Assert.Equal(HttpStatusCode.OK, (await reopened.GetAsync("/api/Auth/Me")).StatusCode);
    }

    [Fact]
    public async Task InactiveSessionRequiresLoginAfterSevenDays()
    {
        var clock = new SessionClock();
        await using var factory = Factory(clock);
        using var client = Client(factory);
        await Login(client, "admin@example.test", "Test-Admin-123!");
        clock.Advance(TimeSpan.FromDays(8));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Auth/Me")).StatusCode);
    }

    [Fact]
    public async Task RenewalsNeverExtendSessionBeyondThirtyDaysFromLogin()
    {
        var clock = new SessionClock();
        var absoluteExpiry = clock.GetUtcNow().AddDays(30);
        await using var factory = Factory(clock);
        using var client = Client(factory);
        await Login(client, "admin@example.test", "Test-Admin-123!");
        foreach (var days in new[] { 6, 6, 6, 6, 5 })
        {
            clock.Advance(TimeSpan.FromDays(days));
            using var response = await client.GetAsync("/api/Auth/Me");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(ReadSessionCookie(response).Expires <= absoluteExpiry);
        }
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Auth/Me")).StatusCode);
    }

    [Fact]
    public async Task RenewedSessionStillHonorsSecurityStampRevocation()
    {
        var clock = new SessionClock();
        await using var factory = Factory(clock);
        using var client = Client(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{Guid.NewGuid()}@example.test";
        var user = new ApplicationUser { Email = email, UserName = email, DisplayName = "Sessione", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        await Login(client, email, Password);
        clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Auth/Me")).StatusCode);
        Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/Auth/Me")).StatusCode);
    }

    private static SetCookieHeaderValue ReadSessionCookie(HttpResponseMessage response)
        => SetCookieHeaderValue.ParseList(response.Headers.GetValues("Set-Cookie").ToList())
            .Single(cookie => cookie.Name == "Fantastiche.Auth");

    [Theory]
    [InlineData("invalid")]
    [InlineData("future")]
    [InlineData("expired")]
    public async Task SessionRejectsInvalidOrExpiredOriginEvenWithUnexpiredTicket(string origin)
    {
        var clock = new SessionClock();
        await using var factory = Factory(clock);
        using var client = Client(factory);
        await RefreshCsrf(client);
        using var login = await client.PostAsJsonAsync("/api/Auth/Login",
            new { email = "admin@example.test", password = "Test-Admin-123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var ticket = options.TicketDataFormat.Unprotect(ReadSessionCookie(login).Value.ToString())!;
        ticket.Properties.Items["Fantastiche.SessionStartedAt"] = origin switch
        {
            "future" => clock.GetUtcNow().AddDays(1).ToString("O", CultureInfo.InvariantCulture),
            "expired" => clock.GetUtcNow().AddDays(-30).ToString("O", CultureInfo.InvariantCulture),
            _ => "invalid"
        };
        using var replay = Client(factory);
        replay.DefaultRequestHeaders.Add("Cookie", $"Fantastiche.Auth={options.TicketDataFormat.Protect(ticket)}");
        using var response = await replay.GetAsync("/api/Auth/Me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(ReadSessionCookie(response).Expires < clock.GetUtcNow());
    }

    [Fact]
    public async Task LegacyCookieRetainsOriginalSessionStartDuringRenewal()
    {
        var clock = new SessionClock();
        var originalStart = clock.GetUtcNow();
        await using var factory = Factory(clock);
        using var client = Client(factory);
        await RefreshCsrf(client);
        using var login = await client.PostAsJsonAsync("/api/Auth/Login",
            new { email = "admin@example.test", password = "Test-Admin-123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var ticket = options.TicketDataFormat.Unprotect(ReadSessionCookie(login).Value.ToString())!;
        ticket.Properties.Items.Remove("Fantastiche.SessionStartedAt");
        ticket.Properties.IsPersistent = false;
        ticket.Properties.ExpiresUtc = originalStart.AddHours(8);
        clock.Advance(TimeSpan.FromHours(1));
        using var legacy = Client(factory);
        legacy.DefaultRequestHeaders.Add("Cookie", $"Fantastiche.Auth={options.TicketDataFormat.Protect(ticket)}");
        using var response = await legacy.GetAsync("/api/Auth/Me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renewed = options.TicketDataFormat.Unprotect(ReadSessionCookie(response).Value.ToString())!;
        Assert.Equal(originalStart, DateTimeOffset.Parse(renewed.Properties.Items["Fantastiche.SessionStartedAt"]!, CultureInfo.InvariantCulture));
        Assert.False(renewed.Properties.IsPersistent);
    }

    private sealed class SessionClock : TimeProvider
    {
        private DateTimeOffset utcNow = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public override DateTimeOffset GetUtcNow() => utcNow;
        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}
