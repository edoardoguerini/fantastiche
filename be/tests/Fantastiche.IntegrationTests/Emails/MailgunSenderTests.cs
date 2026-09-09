using System.Net;
using Fantastiche.Core.Email;
using Fantastiche.Gateways.Mailgun;
using Microsoft.Extensions.Configuration;
namespace Fantastiche.IntegrationTests.Emails;

public sealed class MailgunSenderTests
{
    private static readonly RenderedEmail Email = new("recipient@example.test", "Destinatario", "Invito", "<p>Invito</p>", "Invito");
    private static MailgunEmailSender Sender(HttpMessageHandler handler) => new(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Mailgun:ApiKey"] = "fake-test-key",
        ["Mailgun:Domain"] = "mail.example.test",
        ["Mailgun:From"] = "Fantastiche <test@example.test>",
        ["Mailgun:Region"] = "EU"
    }).Build());
    [Fact]
    public async Task SendsEncodedPayloadToEuRegionAndReturnsProviderAcceptance()
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal("https://api.eu.mailgun.net/v3/mail.example.test/messages", request.RequestUri!.ToString());
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            var form = await request.Content!.ReadAsStringAsync(); Assert.Contains("to=recipient%40example.test", form); Assert.Contains("subject=Invito", form);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"provider-id\"}") };
        });
        var result = await Sender(handler).SendAsync(Email); Assert.Equal("provider-id", result.ProviderMessageId);
    }
    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(401, false)]
    public async Task ClassifiesProviderFailureWithoutExposingResponseBody(int status, bool transient)
    {
        var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("sensitive-provider-body") }));
        var error = await Assert.ThrowsAsync<EmailDeliveryException>(() => Sender(handler).SendAsync(Email));
        Assert.Equal(transient, error.Transient); Assert.DoesNotContain("sensitive-provider-body", error.ToString());
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
