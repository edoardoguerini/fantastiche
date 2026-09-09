using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class AcceptInvitationCommandHandler(LeagueWorkflow workflow) : IRequestHandler<AcceptInvitationCommand, AcceptanceDetails>
{
    public Task<AcceptanceDetails> HandleAsync(AcceptInvitationCommand request, CancellationToken ct) => workflow.AcceptInvitationAsync(request, ct);
}
