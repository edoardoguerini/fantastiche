namespace Fantastiche.Core.Email;

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(
        RenderedEmail email,
        CancellationToken cancellationToken = default);
}
