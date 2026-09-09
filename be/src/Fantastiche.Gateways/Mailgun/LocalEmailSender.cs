using System.Text.Json;
using Fantastiche.Core.Email;
using Microsoft.Extensions.Configuration;
namespace Fantastiche.Gateways.Mailgun;

public sealed class LocalEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(RenderedEmail email, CancellationToken ct = default)
    {
        var directory = configuration["Email:LocalDirectory"] ?? ".local/mail";
        Directory.CreateDirectory(directory);
        var id = Guid.CreateVersion7().ToString();
        var path = Path.Combine(directory, id + ".json");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, options);
        await JsonSerializer.SerializeAsync(stream, email, cancellationToken: ct);
        return new EmailSendResult("local:" + id);
    }
}
