using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Fantastiche.Core.Email;
using Microsoft.Extensions.Configuration;
namespace Fantastiche.Gateways.Mailgun;

public sealed class MailgunEmailSender(HttpClient client, IConfiguration configuration) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(RenderedEmail email, CancellationToken ct = default)
    {
        var key = configuration["Mailgun:ApiKey"] ?? throw new InvalidOperationException("Mailgun ApiKey mancante.");
        var domain = configuration["Mailgun:Domain"] ?? throw new InvalidOperationException("Mailgun Domain mancante.");
        var from = configuration["Mailgun:From"] ?? throw new InvalidOperationException("Mailgun From mancante.");
        var region = configuration["Mailgun:Region"] ?? "EU";
        if (region is not ("EU" or "US")) throw new InvalidOperationException("Regione Mailgun non valida.");
        var host = region == "EU" ? "https://api.eu.mailgun.net" : "https://api.mailgun.net";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{host}/v3/{Uri.EscapeDataString(domain)}/messages");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("api:" + key)));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["from"] = from,
            ["to"] = email.ToAddress,
            ["subject"] = email.Subject,
            ["html"] = email.HtmlBody,
            ["text"] = email.TextBody ?? ""
        });
        using var response = await client.SendAsync(request, ct);
        // Non includere body o header provider nelle eccezioni: possono contenere dati personali.
        if (!response.IsSuccessStatusCode) throw new EmailDeliveryException((int)response.StatusCode >= 500 || (int)response.StatusCode == 429);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return new EmailSendResult(json.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null);
    }
}
