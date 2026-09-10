using FluentValidation;
namespace Fantastiche.Application.Modules.Leagues;

public sealed record LeagueListRequest(int Page = 1, int PageSize = 20);
public sealed class LeagueListRequestValidator : AbstractValidator<LeagueListRequest>
{
    public LeagueListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed record CreateLeagueRequest(string Name, string SeasonName, string OrganizerEmail, string OrganizerName,
 int Budget = 500, int Goalkeepers = 3, int Defenders = 8, int Midfielders = 8, int Forwards = 6);
public sealed class CreateLeagueRequestValidator : AbstractValidator<CreateLeagueRequest>
{
    public CreateLeagueRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SeasonName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.OrganizerEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.OrganizerName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Budget).InclusiveBetween(1, 1000000);
        RuleFor(x => x.Goalkeepers).InclusiveBetween(0, 100);
        RuleFor(x => x.Defenders).InclusiveBetween(0, 100);
        RuleFor(x => x.Midfielders).InclusiveBetween(0, 100);
        RuleFor(x => x.Forwards).InclusiveBetween(0, 100);
    }
}
public sealed record InviteMemberRequest(Guid LeagueSeasonId, string Email, string DisplayName);
public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.LeagueSeasonId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(150);
    }
}
