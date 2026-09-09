using FluentValidation;

namespace Fantastiche.Application.Modules.Leagues;

public sealed record SetLeagueCatalogRequest(Guid ListVersionId);
public sealed class SetLeagueCatalogRequestValidator : AbstractValidator<SetLeagueCatalogRequest>
{
    public SetLeagueCatalogRequestValidator()
    {
        RuleFor(x => x.ListVersionId).NotEmpty();
    }
}
