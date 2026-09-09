namespace Fantastiche.Core.Email;

public sealed record RenderedEmail(
    string ToAddress,
    string? ToName,
    string Subject,
    string HtmlBody,
    string? TextBody);
