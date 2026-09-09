using FluentValidation;

namespace Fantastiche.Application.Modules.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty()
            .WithErrorCode("validation.email")
            .MaximumLength(256)
            .WithErrorCode("validation.email")
            .EmailAddress()
            .WithErrorCode("validation.email");
        RuleFor(request => request.Password)
            .NotEmpty()
            .WithErrorCode("validation.password")
            .MaximumLength(1024)
            .WithErrorCode("validation.password");
    }
}
