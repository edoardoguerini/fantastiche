using System.Text.Json;
using Fantastiche.Core.Email;
using Microsoft.AspNetCore.DataProtection;
namespace Fantastiche.Infrastructure.Emails;

public sealed class EmailPayloadProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("Fantastiche.EmailPayload.v1");
    public string Protect(RenderedEmail email) => protector.Protect(JsonSerializer.Serialize(email));
    public RenderedEmail Unprotect(string payload) => JsonSerializer.Deserialize<RenderedEmail>(protector.Unprotect(payload))
     ?? throw new InvalidOperationException("Payload email non valido.");
}
