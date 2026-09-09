using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class RevokeInvitationCommandHandler(LeagueWorkflow workflow) : IRequestHandler<RevokeInvitationCommand, bool>
{
    public Task<bool> HandleAsync(RevokeInvitationCommand request, CancellationToken ct) => workflow.RevokeInvitationAsync(request, ct);
}
