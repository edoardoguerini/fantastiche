using FluentValidation;
namespace Fantastiche.Application.Modules.Invitations;

public sealed record AcceptInvitationRequest(string Token, string? Password, string? TeamName, string? DisplayName = null);
public sealed class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().Length(64).Matches("^[0-9A-Fa-f]+$");
        RuleFor(x => x.Password).MaximumLength(128);
        RuleFor(x => x.TeamName).MaximumLength(100);
        RuleFor(x => x.DisplayName).MaximumLength(150);
    }
}
